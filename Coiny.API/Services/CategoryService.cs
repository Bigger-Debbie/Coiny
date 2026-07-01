using Coiny.API.Data;
using Coiny.API.DTOs.Categories;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class CategoryService : ICategoryService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CategoryService (CoinyContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync()
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        return await _context.Categories
            .Where(c => c.HouseholdId == householdId)
            .OrderBy(c => c.CategoryType)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                CategoryType = c.CategoryType,
                IsSystem = c.IsSystem,
                IsActive = c.IsActive
            })
            .ToListAsync();
    }

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        var categoryExists = await _context.Categories
            .AnyAsync(c => 
                c.HouseholdId == householdId &&
                c.Name.ToLower() == request.Name.ToLower());

        if (categoryExists)
            throw new InvalidOperationException("A category with this name already exists");

        var category = new Category
        {
            HouseholdId = householdId,
            Name = request.Name,
            CategoryType = request.CategoryType,
            IsSystem = false,
            IsActive = true
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            CategoryType = category.CategoryType,
            IsSystem = category.IsSystem,
            IsActive = category.IsActive
        };
    }
}