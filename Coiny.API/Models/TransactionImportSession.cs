namespace Coiny.API.Models;
public class TransactionImportSession : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public Guid SubmissionId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
