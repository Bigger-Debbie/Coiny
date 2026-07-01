using System.ComponentModel.DataAnnotations;

namespace Coiny.API.DTOs.Transactions;

public class CreateTransactionRequest
{
    [Required]
    public int AccountId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    [MaxLength(250)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Merchant { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
    public bool IsCleared { get; set; }
}