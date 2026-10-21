using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class CreateTransferRequest
{
    [Range(1, int.MaxValue)] public int? ExistingTransactionId { get; set; }
    [Range(0, int.MaxValue)] public int? ExpectedExistingEditVersion { get; set; }
    public bool CreateBothEntries { get; set; }
    [Range(1, int.MaxValue)] public int FromAccountId { get; set; }
    [Range(1, int.MaxValue)] public int ToAccountId { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal Amount { get; set; }
    [Required] public DateOnly? Date { get; set; }
    [MaxLength(250)] public string? Description { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsCleared { get; set; }
}

public class CreateTransferEntryRequest
{
    [Range(1, int.MaxValue)] public int AccountId { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal Amount { get; set; }
    [Required] public DateOnly? Date { get; set; }
    [Required, EnumDataType(typeof(TransferDirection))] public TransferDirection? Direction { get; set; }
    [EnumDataType(typeof(TransferCounterparty))] public TransferCounterparty Counterparty { get; set; }
    [Required, MaxLength(250)] public string Description { get; set; } = "";
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsCleared { get; set; }
}

public class LinkTransferRequest
{
    [Range(0, int.MaxValue)] public int? ExpectedFirstEditVersion { get; set; }
    [Range(0, int.MaxValue)] public int? ExpectedSecondEditVersion { get; set; }
    [Range(1, int.MaxValue)] public int FirstTransactionId { get; set; }
    [Range(1, int.MaxValue)] public int SecondTransactionId { get; set; }
}

public record TransferPairResponse(TransactionResponse Outgoing, TransactionResponse Incoming);

public class CreateTransferCounterpartRequest
{
    [Range(1, int.MaxValue)] public int AccountId { get; set; }
    [Required] public DateOnly? Date { get; set; }
    [System.Text.Json.Serialization.JsonRequired, Range(0, int.MaxValue)] public int ExpectedEditVersion { get; set; }
}
