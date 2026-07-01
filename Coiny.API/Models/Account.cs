using Coiny.API.Enums;

namespace Coiny.API.Models;

public class Account : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int? InstitutionId { get; set; }
    public Institution? Institution { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}