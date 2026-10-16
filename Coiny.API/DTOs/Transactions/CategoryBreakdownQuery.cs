using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class CategoryBreakdownQuery
{
    [Range(1, int.MaxValue)]
    public int? AccountId { get; set; }
    [Required]
    public DateOnly? StartDate { get; set; }
    [Required]
    public DateOnly? EndDate { get; set; }
    [Required, EnumDataType(typeof(CategoryType))]
    public CategoryType? CategoryType { get; set; }
}
