using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Savings;
public class SavingsComponent
{
    public Guid Key { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "Target"; // Target, Recurring, Monthly
    public decimal Amount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public int RepeatMonths { get; set; } = 12;
    // Snapshot of money already earmarked when this contribution plan starts.
    public decimal AlreadyFunded { get; set; }
}
public class SavingsPlan
{
    public DateOnly StartDate { get; set; }
    public List<SavingsComponent> Components { get; set; } = [];
}
public class SaveSavingsFund
{
    public bool AcceptShortfall { get; set; }
    public Guid? SourceKey { get; set; }
    [JsonRequired] public int AccountId { get; set; }
    public string Name { get; set; } = "";
    [JsonRequired] public int ExpectedEditVersion { get; set; }
    public int? ExpectedRevisionId { get; set; }
    public int? DraftId { get; set; }
    public int? DraftEditVersion { get; set; }
    public SavingsPlan Plan { get; set; } = new();
    // Only on creation: describe existing money, without creating bank transactions.
    public Dictionary<Guid, decimal> OpeningAllocations { get; set; } = [];
}
public record SavingsComponentView(SavingsComponent Definition, decimal Balance);
public record SavingsActivityView(int Id, Guid ComponentKey, DateOnly Date, decimal Amount, string Kind, string? Note, int? TransactionId, string? Merchant, bool MissingTransaction, Guid OperationId);
public record SavingsFundView(int Id, Guid SourceKey, int AccountId, string Name, int EditVersion, bool IsArchived,
    SavingsPlan Plan, decimal Balance, IReadOnlyList<SavingsComponentView> Components, IReadOnlyList<SavingsActivityView> Activities, DateOnly? NextContributionDate = null, decimal? NextContributionAmount = null);
public record SavingsAccountView(int AccountId, string Name, decimal Balance, decimal Allocated, decimal Unallocated, decimal Shortfall, IReadOnlyList<SavingsFundView> Funds);
public record SavingsPeriodPreview(DateOnly StartDate, DateOnly EndDate, decimal Contribution, decimal AvailableBeforeGoal, decimal Remainder, IReadOnlyDictionary<Guid, decimal> Components, DateOnly DueDate);
public record SavingsFundPreview(decimal MonthlyEquivalent, IReadOnlyList<SavingsPeriodPreview> Periods, decimal LargestShortfall, decimal SupportedMonthlyContribution, DateOnly? EstimatedCompletion, string? Warning, DateOnly? AffordableCompletion = null);
public class SavingsActivityRequest
{
    [JsonRequired] public Guid OperationId { get; set; }
    [JsonRequired] public int ExpectedEditVersion { get; set; }
    public DateOnly Date { get; set; }
    public string Kind { get; set; } = "Contribution";
    public Dictionary<Guid, decimal> Amounts { get; set; } = [];
    public int? TransactionId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Note { get; set; }
}
public class MoveSavingsAllocation
{
    public Guid OperationId { get; set; }
    public int ExpectedEditVersion { get; set; }
    public Guid ComponentKey { get; set; }
    public Guid? DestinationFundKey { get; set; }
    public Guid? DestinationComponentKey { get; set; }
    public int? DestinationEditVersion { get; set; }
    public decimal Amount { get; set; }
}
public record ArchiveSavingsFund(int ExpectedEditVersion,int ExpectedRevisionId,DateOnly EffectiveDate,int? DraftId=null,int? DraftEditVersion=null);
