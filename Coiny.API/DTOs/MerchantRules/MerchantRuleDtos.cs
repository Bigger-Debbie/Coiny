using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coiny.API.Enums;
namespace Coiny.API.DTOs.MerchantRules;
public class MerchantRuleInput {
    [Required] public string MatchType {get;set;} = string.Empty;
    [Required,StringLength(200,MinimumLength=3)] public string Pattern {get;set;} = string.Empty;
    [JsonRequired,Range(1,int.MaxValue)] public int CategoryId {get;set;}
    [JsonRequired] public bool IsActive {get;set;} = true;
    [JsonRequired,Range(0,int.MaxValue)] public int ExpectedEditVersion {get;set;}
}
public record MerchantRuleResponse(int Id,string MatchType,string Pattern,int CategoryId,string CategoryName,CategoryType CategoryType,bool CategoryActive,bool IsActive,int EditVersion);
public record RuleExample(int Id,DateTime Date,string Merchant,string Description,decimal Amount,string AccountName);
public record RulePreview(int TransactionsChecked,IReadOnlyList<RuleExample> Examples);
