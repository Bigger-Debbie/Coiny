using Coiny.API.DTOs.CsvMappings;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
namespace Coiny.API.Tests;
public class SavedCsvMappingsTests {
 private sealed class User(int household=1):ICurrentUserService {public bool IsAuthenticated()=>true;public string? GetUserId()=>"test";public Task<int> GetCurrentHouseholdIdAsync()=>Task.FromResult(household);}
 private static SavedCsvMappingsService Service(BudgetTestDatabase db,int household=1)=>new(db.Context(),new User(household));
 private static SaveCsvMappingRequest Input()=>new(){Name="Checking export",AccountType=AccountType.Checking,Settings=new(){Headers=["Date","Description","Amount"],Mapping=new(){DateColumn=0,DescriptionColumn=1,DateFormat="MM/DD/YY",Amount=new(){Mode="signed",Column=2,PositiveMeans="In"}}}};
 [Fact]public async Task Mappings_round_trip_are_household_scoped_and_require_current_versions(){
  await using var db=await BudgetTestDatabase.CreateAsync();var saved=await Service(db).SaveAsync(null,Input());Assert.Equal(1,saved.EditVersion);Assert.Equal("MM/DD/YY",saved.Settings.Mapping.DateFormat);Assert.Single(await Service(db).ListAsync());Assert.Empty(await Service(db,2).ListAsync());
  await Assert.ThrowsAsync<ArgumentException>(()=>Service(db,2).DeleteAsync(saved.Id,1));await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).DeleteAsync(saved.Id,0));await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).SaveAsync(saved.Id,Input()));
  var input=Input();input.ExpectedEditVersion=1;input.Name="Changed export";var changed=await Service(db).SaveAsync(saved.Id,input);Assert.Equal(2,changed.EditVersion);await Service(db).DeleteAsync(saved.Id,2);Assert.Empty(await Service(db).ListAsync());
 }
 [Fact]public async Task Duplicate_names_and_foreign_institutions_are_rejected(){
  await using var db=await BudgetTestDatabase.CreateAsync();await Service(db).SaveAsync(null,Input());var duplicate=Input();duplicate.Name=" CHECKING EXPORT ";await Assert.ThrowsAsync<BudgetConflictException>(()=>Service(db).SaveAsync(null,duplicate));
  var c=db.Context();c.Institutions.Add(new Institution {Id=1,HouseholdId=2,Name="Other bank"});await c.SaveChangesAsync();var foreign=Input();foreign.InstitutionId=1;foreign.Name="Other export";await Assert.ThrowsAsync<ArgumentException>(()=>Service(db).SaveAsync(null,foreign));
 }
 [Fact]public void Malformed_columns_dates_and_amount_directions_are_not_saved(){
  var input=Input();input.Settings.Mapping.Amount.PositiveMeans="Maybe";Assert.Throws<ArgumentException>(()=>SavedCsvMappingsService.Validate(input.Settings));
  input=Input();input.Settings.Mapping.DescriptionColumn=0;Assert.Throws<ArgumentException>(()=>SavedCsvMappingsService.Validate(input.Settings));
  input=Input();input.Settings.Mapping.Amount.Column=5;Assert.Throws<ArgumentException>(()=>SavedCsvMappingsService.Validate(input.Settings));
  input=Input();input.Settings.Mapping.DateFormat="Auto";Assert.Throws<ArgumentException>(()=>SavedCsvMappingsService.Validate(input.Settings));
  input=Input();input.Settings.Headers[0]="";Assert.Throws<ArgumentException>(()=>SavedCsvMappingsService.Validate(input.Settings));
 }
}
