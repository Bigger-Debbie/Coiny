using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Transactions;

public class MarketAdjustmentRequest
{
    [JsonRequired, Range(1, int.MaxValue)] public int AccountId { get; set; }
    [JsonRequired] public DateOnly Date { get; set; }
    [JsonRequired] public decimal UpdatedBalance { get; set; }
    [JsonRequired] public decimal ExpectedBalance { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsCleared { get; set; }
}
public record MarketAdjustmentBalance(decimal Balance);
