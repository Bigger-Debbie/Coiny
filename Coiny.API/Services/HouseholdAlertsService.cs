using Coiny.API.Data;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public record AlertResponse(int Id,string Merchant,string Frequency,decimal TypicalAmount,DateOnly NextExpected,DateTime UpdatedAt,bool Unread,int Version);
public record AlertsResponse(int UnreadCount,int Total,int Page,IReadOnlyList<AlertResponse> Items);
public class HouseholdAlertsService(IFinancialContext context,ICurrentUserService user)
{
    public async Task<int> CountAsync() {
        var household=await user.GetCurrentHouseholdIdAsync();var uid=user.GetUserId()??throw new UnauthorizedAccessException();
        return await context.HouseholdAlerts.CountAsync(a=>a.HouseholdId==household && a.Active && !context.HouseholdAlertReads.Any(r=>r.AlertId==a.Id && r.UserId==uid && r.Version>=a.Version));
    }
    public async Task<AlertsResponse> ListAsync(int page=1) {
        if(page<1 || page>100000) throw new ArgumentException("Choose a valid alerts page.");
        var household=await user.GetCurrentHouseholdIdAsync();var uid=user.GetUserId()??throw new UnauthorizedAccessException();
        var query=context.HouseholdAlerts.AsNoTracking().Where(a=>a.HouseholdId==household && a.Active);
        var rows=await query.OrderByDescending(a=>a.UpdatedAt).ThenByDescending(a=>a.Id).Skip((page-1)*20).Take(20)
            .Select(a=>new AlertResponse(a.Id,a.Merchant,a.Frequency,a.TypicalAmount,a.NextExpected,a.UpdatedAt,
                !context.HouseholdAlertReads.Any(r=>r.AlertId==a.Id && r.UserId==uid && r.Version>=a.Version),a.Version)).ToListAsync();
        return new(await CountAsync(),await query.CountAsync(),page,rows);
    }
    public async Task ReadAsync(int id,int version) {
        var household=await user.GetCurrentHouseholdIdAsync();var uid=user.GetUserId()??throw new UnauthorizedAccessException();
        var alert=await context.HouseholdAlerts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id && a.HouseholdId==household && a.Active) ?? throw new ArgumentException("This alert is no longer available.");
        if(version<1 || version>alert.Version) throw new ArgumentException("Choose a valid alert version.");
        var read=await context.HouseholdAlertReads.SingleOrDefaultAsync(r=>r.AlertId==id && r.UserId==uid);
        if(read==null) context.Add(new HouseholdAlertRead {AlertId=id,UserId=uid,Version=version});else read.Version=Math.Max(read.Version,version);
        try {await context.SaveChangesAsync();}catch(DbUpdateException) {throw new BudgetConflictException("The alert changed elsewhere. Refresh and try again.");}
    }
}
