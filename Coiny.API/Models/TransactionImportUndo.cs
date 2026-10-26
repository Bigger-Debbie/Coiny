namespace Coiny.API.Models;
public class TransactionImportUndo : BaseEntity
{
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public Guid OperationId { get; set; }
    public string RequestHash { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string BatchIdsJson { get; set; } = "[]";
    public string DetailJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
