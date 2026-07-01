namespace Coiny.API.Models;

public class Household : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<HouseholdMember> HouseboldMembers { get; set; } = new List<HouseholdMember>();
    public ICollection<Institution> Institutions { get; set; } = new List<Institution>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}