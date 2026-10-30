using System.Text;
using System.Text.RegularExpressions;
using Coiny.API.Data;
using Coiny.API.DTOs.MerchantRules;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public class MerchantRulesService(IFinancialContext context,ICurrentUserService user)
{
    public static string Normalize(string text)=>Regex.Replace(text.Normalize(NormalizationForm.FormKC).ToUpperInvariant(),@"[^\p{L}\p{N}]+"," ").Trim();
    private static MerchantRuleResponse Response(MerchantCategoryRule r)=>new(r.Id,r.MatchType,r.Pattern,r.CategoryId,r.Category.Name,r.Category.CategoryType,r.Category.IsActive,r.IsActive,r.EditVersion);
    public async Task<IReadOnlyList<MerchantRuleResponse>> ListAsync() {
        var h=await user.GetCurrentHouseholdIdAsync();var rows=await context.MerchantCategoryRules.AsNoTracking().Include(r=>r.Category).Where(r=>r.HouseholdId==h).OrderBy(r=>r.Pattern).ThenBy(r=>r.Id).ToListAsync();return rows.Select(Response).ToArray();
    }
    private async Task<Category> Validate(int household,MerchantRuleInput input,bool allowArchived=false) {
        if(input.MatchType is not ("MerchantExact" or "DescriptionContains"))throw new ArgumentException("Choose exact merchant or description contains.");
        var normalized=Normalize(input.Pattern.Trim());
        if(input.Pattern.Trim().Length>200 || normalized.Length is <3 or >200)throw new ArgumentException("Enter a matching phrase of 3 to 200 letters or numbers.");
        return await context.Categories.SingleOrDefaultAsync(c=>c.HouseholdId==household && c.Id==input.CategoryId && c.CategoryType!=CategoryType.Transfer && (c.IsActive || allowArchived)) ?? throw new ArgumentException("Choose an active Income or Expense category in this household.");
    }
    public async Task<MerchantRuleResponse> SaveAsync(int? id,MerchantRuleInput input) {
        var h=await user.GetCurrentHouseholdIdAsync();
        var r=id.HasValue ? await context.MerchantCategoryRules.Include(r=>r.Category).SingleOrDefaultAsync(r=>r.HouseholdId==h && r.Id==id) ?? throw new ArgumentException("Rule not found in this household.") : new MerchantCategoryRule {HouseholdId=h};
        if(r.EditVersion!=input.ExpectedEditVersion)throw new BudgetConflictException("This rule changed elsewhere. Reload it before saving.");
        var category=await Validate(h,input,!input.IsActive && id.HasValue && r.CategoryId==input.CategoryId);
        var normalized=Normalize(input.Pattern.Trim());
        if(await context.MerchantCategoryRules.AnyAsync(other=>other.HouseholdId==h && other.Id!=r.Id && other.MatchType==input.MatchType && other.NormalizedPattern==normalized))throw new BudgetConflictException("A rule with this matching phrase already exists. Edit that rule instead.");
        r.MatchType=input.MatchType;r.Pattern=input.Pattern.Trim();r.NormalizedPattern=normalized;r.CategoryId=category.Id;r.Category=category;r.IsActive=input.IsActive;r.EditVersion++;
        if(!id.HasValue)context.Add(r);
        try {await context.SaveChangesAsync();}catch(DbUpdateException){throw new BudgetConflictException("This rule changed elsewhere or duplicates another rule. Reload and try again.");}
        return Response(r);
    }
    public async Task DeleteAsync(int id,int version) {
        var h=await user.GetCurrentHouseholdIdAsync();var r=await context.MerchantCategoryRules.SingleOrDefaultAsync(r=>r.HouseholdId==h && r.Id==id) ?? throw new ArgumentException("Rule not found in this household.");
        if(r.EditVersion!=version)throw new BudgetConflictException("This rule changed elsewhere. Reload before deleting.");
        context.Remove(r);try{await context.SaveChangesAsync();}catch(DbUpdateException){throw new BudgetConflictException("This rule changed elsewhere. Reload before deleting.");}
    }
    public async Task<RulePreview> PreviewAsync(MerchantRuleInput input) {
        var h=await user.GetCurrentHouseholdIdAsync();var category=await Validate(h,input);
        var rows=await context.Transactions.AsNoTracking().Where(t=>t.HouseholdId==h && t.Category.CategoryType==category.CategoryType)
            .OrderByDescending(t=>t.TransactionDate).ThenByDescending(t=>t.Id).Take(1000)
            .Select(t=>new RuleExample(t.Id,t.TransactionDate,t.Merchant??"",t.Description,t.Amount,t.Account.Name)).ToListAsync();
        var pattern=Normalize(input.Pattern);return new(rows.Count,rows.Where(t=>input.MatchType=="MerchantExact"?Normalize(t.Merchant)==pattern:Normalize(t.Description).Contains(pattern,StringComparison.Ordinal)).Take(10).ToArray());
    }
}
