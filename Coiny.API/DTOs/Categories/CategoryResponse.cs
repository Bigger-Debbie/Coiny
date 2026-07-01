using Coiny.API.Enums;

namespace Coiny.API.DTOs.Categories;

public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
}