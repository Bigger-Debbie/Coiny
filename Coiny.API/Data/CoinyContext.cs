using Coiny.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data;

public class CoinyContext : IdentityDbContext<ApplicationUser>
{
    public CoinyContext(DbContextOptions<CoinyContext> options) : base(options) { }

    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<AccountReconciliation> AccountReconciliations => Set<AccountReconciliation>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionImportSession> TransactionImportSessions => Set<TransactionImportSession>();
    public DbSet<TransactionImportBatch> TransactionImportBatches => Set<TransactionImportBatch>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<HouseholdBudget> HouseholdBudgets => Set<HouseholdBudget>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<BudgetIncomeSource> BudgetIncomeSources => Set<BudgetIncomeSource>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<BudgetCategoryRule> BudgetCategoryRules => Set<BudgetCategoryRule>();
    public DbSet<TransactionBudgetAssignment> TransactionBudgetAssignments => Set<TransactionBudgetAssignment>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        // Stage invalidation with the ledger write so a failed transaction also rolls back its checkpoint status.
        ChangeTracker.DetectChanges();
        var affected = new Dictionary<int, DateOnly>();
        void Affect(int id, DateOnly date) { if (id > 0 && (!affected.TryGetValue(id, out var old) || date < old)) affected[id] = date; }
        var fields = new[] { nameof(Transaction.AccountId), nameof(Transaction.TransactionDate), nameof(Transaction.Amount), nameof(Transaction.CategoryId), nameof(Transaction.TransferDirection), nameof(Transaction.Description), nameof(Transaction.Merchant), nameof(Transaction.Notes), nameof(Transaction.IsCleared) };
        foreach (var entry in ChangeTracker.Entries<Transaction>().ToList())
        {
            var changed = entry.State == EntityState.Modified && fields.Any(f => entry.Property(f).IsModified && !Equals(entry.Property(f).OriginalValue, entry.Property(f).CurrentValue));
            if (entry.State == EntityState.Added || changed) Affect(entry.Entity.AccountId, DateOnly.FromDateTime(entry.Entity.TransactionDate));
            if (entry.State == EntityState.Deleted || changed) Affect(entry.OriginalValues.GetValue<int>(nameof(Transaction.AccountId)), DateOnly.FromDateTime(entry.OriginalValues.GetValue<DateTime>(nameof(Transaction.TransactionDate))));
        }
        foreach (var entry in ChangeTracker.Entries<Account>().Where(e => e.State == EntityState.Modified).ToList())
            if (new[] { nameof(Account.OpeningBalance), nameof(Account.OpeningBalanceDate), nameof(Account.AccountType) }.Any(f => entry.Property(f).IsModified && !Equals(entry.Property(f).OriginalValue, entry.Property(f).CurrentValue))) Affect(entry.Entity.Id, DateOnly.MinValue);
        if (affected.Count > 0)
        {
            var ids = affected.Keys.ToArray();
            var checkpoints = await AccountReconciliations.Where(r => ids.Contains(r.AccountId) && r.InvalidatedAt == null).ToListAsync(cancellationToken);
            foreach (var record in checkpoints.Where(r => r.StatementDate >= affected[r.AccountId]))
            {
                record.InvalidatedAt = DateTime.UtcNow;
                record.InvalidationReason = affected[record.AccountId] == DateOnly.MinValue ? "The account opening balance or date changed." : "Transactions in this verified period changed.";
            }
        }
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CoinyContext).Assembly);
    }
}
