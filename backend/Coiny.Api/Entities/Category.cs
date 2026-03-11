using System.ComponentModel.DataAnnotations;

namespace Coiny.Api.Entities;

public class Category
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
