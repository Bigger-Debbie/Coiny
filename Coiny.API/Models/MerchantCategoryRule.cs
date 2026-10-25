namespace Coiny.API.Models;
public class MerchantCategoryRule : BaseEntity
{
    public int HouseholdId {get;set;}
    public Household Household {get;set;} = null!;
    public string MatchType {get;set;} = string.Empty;
    public string Pattern {get;set;} = string.Empty;
    public string NormalizedPattern {get;set;} = string.Empty;
    public int CategoryId {get;set;}
    public Category Category {get;set;} = null!;
    public bool IsActive {get;set;} = true;
    public int EditVersion {get;set;}
}
