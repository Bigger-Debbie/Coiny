using System.ComponentModel.DataAnnotations;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class TransactionQuery
{
    [Range(1, int.MaxValue)]
    public int? AccountId { get; set; }
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }
    [EnumDataType(typeof(CategoryType))]
    public CategoryType? CategoryType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [MaxLength(100)]
    public string? Search { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
