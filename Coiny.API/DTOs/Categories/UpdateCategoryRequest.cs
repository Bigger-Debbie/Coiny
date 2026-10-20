using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Coiny.API.DTOs.Categories;
public class UpdateCategoryRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [JsonRequired] public bool IsActive { get; set; }
}
