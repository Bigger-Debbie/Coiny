using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coiny.API.Enums;
namespace Coiny.API.DTOs.CsvMappings;
public class CsvAmountMapping {
    public string Mode {get;set;}=string.Empty;
    public int? Column {get;set;}
    public int? DebitColumn {get;set;}
    public int? CreditColumn {get;set;}
    public string? PositiveMeans {get;set;}
    public int? DirectionColumn {get;set;}
    public Dictionary<string,string>? Directions {get;set;}
}
public class CsvColumnMapping {
    [JsonRequired]public int DateColumn {get;set;}
    [JsonRequired]public int DescriptionColumn {get;set;}
    public string DateFormat {get;set;}=string.Empty;
    [Required]public CsvAmountMapping Amount {get;set;}=new();
    public string DecimalSeparator {get;set;}=".";
    public string[] ExcludedStatuses {get;set;}=[];
    public int? StatusColumn {get;set;}
    public int? ClassificationColumn {get;set;}
    public int? BalanceColumn {get;set;}
    public int? MerchantColumn {get;set;}
    public int? AccountColumn {get;set;}
}
public class CsvMappingSettings {
    [Required]public string[] Headers {get;set;}=[];
    [Required]public CsvColumnMapping Mapping {get;set;}=new();
    public string Encoding {get;set;}="utf-8";
    public string Delimiter {get;set;}=",";
    public int HeaderLine {get;set;}=1;
    public string? BalanceOrder {get;set;}
}
public class SaveCsvMappingRequest {
    [Required,StringLength(100)]public string Name {get;set;}=string.Empty;
    [JsonRequired]public AccountType AccountType {get;set;}
    public int? InstitutionId {get;set;}
    [Required]public CsvMappingSettings Settings {get;set;}=new();
    [JsonRequired,Range(0,int.MaxValue)]public int ExpectedEditVersion {get;set;}
}
public record SavedCsvMappingResponse(int Id,string Name,AccountType AccountType,int? InstitutionId,CsvMappingSettings Settings,int EditVersion);
