using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Accounts;

public class CreateAccountRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public AccountType AccountType { get; set; }
    public int? InstitutionId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OpeningBalance { get; set; }
}