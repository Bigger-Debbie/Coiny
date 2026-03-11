using System.ComponentModel.DataAnnotations;

namespace Coiny.Api.Entities;

public class Transaction
{
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Merchant { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = "Manual";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
