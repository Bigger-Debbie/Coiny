using System.ComponentModel.DataAnnotations;

namespace Coiny.API.DTOs.Households;

public class CreateHouseholdRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}