using Coiny.API.Data.Configurations;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

// Central Identity/orchestration model with its own PostgreSQL migration chain.
public sealed class ControlContext(DbContextOptions<ControlContext> options) : IdentityDbContext<ApplicationUser>(options), IControlContext
{
    public DbSet<HouseholdDatabase> HouseholdDatabases => Set<HouseholdDatabase>();

    public DbSet<AuthRecoveryCode> AuthRecoveryCodes => Set<AuthRecoveryCode>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new UserPreferenceConfiguration());
        builder.ApplyConfiguration(new AuthRecoveryCodeConfiguration());
        builder.ApplyConfiguration(new AuthSessionConfiguration());
        builder.ApplyConfiguration(new HouseholdConfiguration());
        builder.ApplyConfiguration(new HouseholdMemberConfiguration());
        builder.ApplyConfiguration(new HouseholdInvitationConfiguration());
        foreach (var type in StorageModelOwnership.Financial.Keys) builder.Ignore(type);
        builder.Ignore<BudgetSchedule>();
        StorageModelOwnership.NameTables(builder, StorageModelOwnership.Control);
        builder.Entity<InstallationMaintenanceState>(entity => {
            entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.ToTable("InstallationMaintenance", table => table.HasCheckConstraint("CK_InstallationMaintenance_Singleton", "\"Id\"=1"));
            entity.HasData(new InstallationMaintenanceState { Id = 1 });
        });
        builder.Entity<LegacyImportState>(entity => {
            entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.SourceSha256).HasMaxLength(64);
            entity.ToTable("LegacyImportState");
        });
        builder.Entity<PendingPreferenceChange>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("\"CompletedAt\" IS NULL");
            entity.ToTable("PendingPreferenceChanges");
        });
        builder.Entity<HouseholdDatabase>(entity => {
            entity.HasKey(x => x.HouseholdId);
            entity.HasIndex(x => x.StorageKey).IsUnique();
            entity.Property(x => x.OwnerNode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.State).IsConcurrencyToken();
            entity.HasOne<Household>().WithOne().HasForeignKey<HouseholdDatabase>(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("HouseholdDatabases", table => {
                table.HasCheckConstraint("CK_HouseholdDatabases_State", "\"State\" BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_HouseholdDatabases_Schema", "\"SchemaVersion\" >= 0");
            });
        });
    }
}
