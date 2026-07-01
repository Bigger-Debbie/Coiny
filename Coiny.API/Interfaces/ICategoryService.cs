using Coiny.API.DTOs.Categories;

namespace Coiny.API.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetCategoriesAsync();
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request);
}