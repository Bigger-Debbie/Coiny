using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.CsvMappings;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public class SavedCsvMappingsService(CoinyContext context,ICurrentUserService user) {
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static SavedCsvMappingResponse Response(SavedCsvMapping p)=>new(p.Id,p.Name,p.AccountType,p.InstitutionId,JsonSerializer.Deserialize<CsvMappingSettings>(p.SettingsJson,Json)!,p.EditVersion);
    public async Task<IReadOnlyList<SavedCsvMappingResponse>> ListAsync(){var h=await user.GetCurrentHouseholdIdAsync();var rows=await context.SavedCsvMappings.AsNoTracking().Where(p=>p.HouseholdId==h).OrderBy(p=>p.Name).ToListAsync();return rows.Select(Response).ToArray();}
    public static void Validate(CsvMappingSettings s) {
        if(s.Headers is null || s.Headers.Length is <2 or >100 || s.Headers.Any(h=>h is null || h.Length>200))throw new ArgumentException("Save a CSV header with 2–100 columns of up to 200 characters.");
        if(s.Encoding is not ("utf-8" or "windows-1252" or "utf-16le") || s.Delimiter is not ("," or ";" or "\t") || s.HeaderLine is <1 or >100 || s.BalanceOrder is not (null or "NewestFirst" or "OldestFirst"))throw new ArgumentException("Choose valid CSV file-format options.");
        var m=s.Mapping ?? throw new ArgumentException("Choose CSV column mappings.");var a=m.Amount ?? throw new ArgumentException("Choose an amount layout.");
        var formats=new[]{"MM/DD/YYYY","MM/DD/YY","DD/MM/YYYY","DD/MM/YY","YYYY-MM-DD","MM-DD-YYYY","MM-DD-YY","DD-MM-YYYY","DD-MM-YY","DD.MM.YYYY","DD.MM.YY","YYYYMMDD"};
        if(!formats.Contains(m.DateFormat) || m.DecimalSeparator is not ("." or ","))throw new ArgumentException("Choose a valid date and number format.");
        var cols=new List<int?> {m.DateColumn,m.DescriptionColumn,m.StatusColumn,m.ClassificationColumn,m.BalanceColumn,m.MerchantColumn,m.AccountColumn};
        if(a.Mode=="split") {if(a.DebitColumn is null || a.CreditColumn is null)throw new ArgumentException("Choose debit and credit columns.");cols.Add(a.DebitColumn);cols.Add(a.CreditColumn);}
        else if(a.Mode=="signed") {if(a.Column is null || a.PositiveMeans is not ("In" or "Out"))throw new ArgumentException("Confirm the signed amount direction.");cols.Add(a.Column);}
        else if(a.Mode=="indicator") {if(a.Column is null || a.DirectionColumn is null || a.Directions is null || a.Directions.Count is <1 or >50 || a.Directions.Any(p=>p.Key.Length is <1 or >100 || p.Value is not ("In" or "Out")))throw new ArgumentException("Choose a valid debit/credit indicator mapping.");cols.Add(a.Column);cols.Add(a.DirectionColumn);}
        else throw new ArgumentException("Choose a valid amount layout.");
        if(cols.Where(c=>c.HasValue).Any(c=>c<0 || c>=s.Headers.Length) || cols.Where(c=>c.HasValue).Distinct().Count()!=cols.Count(c=>c.HasValue))throw new ArgumentException("Choose different valid columns for each mapped field.");
        if(cols.Where(c=>c.HasValue).Any(c=>string.IsNullOrWhiteSpace(s.Headers[c!.Value])))throw new ArgumentException("Mapped columns need nonempty header names.");
        if(m.ExcludedStatuses is null || m.ExcludedStatuses.Length>50 || m.ExcludedStatuses.Any(v=>v is null || v.Length>100))throw new ArgumentException("Choose up to 50 posting statuses.");
    }
    public async Task<SavedCsvMappingResponse> SaveAsync(int? id,SaveCsvMappingRequest input) {
        var h=await user.GetCurrentHouseholdIdAsync();if(string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length>100 || !Enum.IsDefined(input.AccountType))throw new ArgumentException("Name this mapping and choose a valid account type.");
        Validate(input.Settings);
        if(input.InstitutionId.HasValue && !await context.Institutions.AnyAsync(i=>i.Id==input.InstitutionId && i.HouseholdId==h))throw new ArgumentException("Choose an institution in this household.");
        var row=id.HasValue?await context.SavedCsvMappings.SingleOrDefaultAsync(p=>p.Id==id && p.HouseholdId==h)??throw new ArgumentException("Mapping not found in this household."):new SavedCsvMapping {HouseholdId=h};
        if(row.EditVersion!=input.ExpectedEditVersion)throw new BudgetConflictException("This mapping changed elsewhere. Reload it before saving.");
        var name=input.Name.Trim().ToUpperInvariant();if(await context.SavedCsvMappings.AnyAsync(p=>p.HouseholdId==h && p.Id!=row.Id && p.NormalizedName==name))throw new BudgetConflictException("A mapping with this name already exists. Choose another name or update that mapping.");
        if(!id.HasValue && await context.SavedCsvMappings.CountAsync(p=>p.HouseholdId==h)>=100)throw new ArgumentException("This household already has 100 saved mappings. Delete an unused one first.");
        row.Name=input.Name.Trim();row.NormalizedName=name;row.AccountType=input.AccountType;row.InstitutionId=input.InstitutionId;row.SettingsJson=JsonSerializer.Serialize(input.Settings,Json);row.EditVersion++;
        if(!id.HasValue)context.Add(row);try{await context.SaveChangesAsync();}catch(DbUpdateException){throw new BudgetConflictException("This mapping changed elsewhere or uses an existing name. Reload and try again.");}return Response(row);
    }
    public async Task DeleteAsync(int id,int version){var h=await user.GetCurrentHouseholdIdAsync();var row=await context.SavedCsvMappings.SingleOrDefaultAsync(p=>p.Id==id && p.HouseholdId==h)??throw new ArgumentException("Mapping not found in this household.");if(row.EditVersion!=version)throw new BudgetConflictException("This mapping changed elsewhere. Reload before deleting.");context.Remove(row);try{await context.SaveChangesAsync();}catch(DbUpdateException){throw new BudgetConflictException("This mapping changed elsewhere. Reload before deleting.");}}
}
