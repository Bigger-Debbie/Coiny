using Coiny.API.Enums;
namespace Coiny.API.Models;
public class SavedCsvMapping : BaseEntity {
    public int HouseholdId {get;set;}
    public Household Household {get;set;}=null!;
    public string Name {get;set;}=string.Empty;
    public string NormalizedName {get;set;}=string.Empty;
    public AccountType AccountType {get;set;}
    public int? InstitutionId {get;set;}
    public Institution? Institution {get;set;}
    public string SettingsJson {get;set;}=string.Empty;
    public int EditVersion {get;set;}
}
