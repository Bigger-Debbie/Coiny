using System.ComponentModel.DataAnnotations;

namespace Coiny.API.DTOs.Institutions;

public class CreateInstitutionRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Website { get; set; }
}