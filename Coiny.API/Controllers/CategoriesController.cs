using Microsoft.EntityFrameworkCore;
using Coiny.API.DTOs.Categories;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[BudgetApiExceptionFilter]
[Route("api/[controller]")]
[Coiny.API.Data.Hybrid.HouseholdStorage]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController (ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories()
    {
        var categories = await _categoryService.GetCategoriesAsync();

        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CreateCategoryRequest>> CreateCategory(CreateCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.CreateCategoryAsync(request);

            return CreatedAtAction(
                nameof(GetCategories),
                new { id = category.Id },
                category);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

    }
    [HttpPut("{id:int:min(1)}")]
    public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryRequest request,
        [FromServices] Coiny.API.Data.IFinancialContext context,
        [FromServices] ICurrentUserService user)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        var category = await context.Categories.SingleOrDefaultAsync( c => c.Id == id && c.HouseholdId == household);
        if (category is null) return NotFound();
        if (category.IsSystem || category.CategoryType == Coiny.API.Enums.CategoryType.Transfer)
            return BadRequest("System categories cannot be changed.");
        var name = request.Name.Trim();
        if (name.Length is < 1 or > 100) return BadRequest("Enter a category name of up to 100 characters.");
        if (await context.Categories.AnyAsync(
            c => c.HouseholdId == household && c.Id != id && c.Name.ToLower() == name.ToLower()))
            return BadRequest("A category with this name already exists.");
        category.Name = name;
        category.IsActive = request.IsActive;
        await context.SaveChangesAsync();
        return Ok(new CategoryResponse { Id = category.Id, Name = category.Name, CategoryType = category.CategoryType,
            IsSystem = category.IsSystem, IsActive = category.IsActive });
    }
}