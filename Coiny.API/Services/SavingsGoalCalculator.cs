using Coiny.API.DTOs.Savings;
namespace Coiny.API.Services;

// Cumulative rounding means adjacent periods add to the exact goal, including odd cents.
public static class SavingsGoalCalculator
{
    public static void Validate(SavingsPlan plan)
    {
        if (plan.StartDate <= DateOnly.MinValue || plan.StartDate.Year > 9900 || plan.Components is null || plan.Components.Count is < 1 or > 30)
            throw new ArgumentException("Choose a start date and between 1 and 30 savings components.");
        if (plan.Components.Any(c => c is null) || plan.Components.Select(c => c.Key).Distinct().Count() != plan.Components.Count)
            throw new ArgumentException("Each component needs its own key.");
        foreach (var c in plan.Components)
        {
            if (c.Key == Guid.Empty || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 100 || c.Mode is not ("Target" or "Recurring" or "Monthly"))
                throw new ArgumentException("Give each component a name and a supported goal type.");
            Money(c.Amount); Money(c.AlreadyFunded);
            if (c.Amount <= 0 && c.Mode != "Monthly") throw new ArgumentException("Each goal needs a positive amount.");
            if (c.Mode != "Monthly" && (c.TargetDate is null || c.TargetDate < plan.StartDate && (c.Mode != "Target" || c.AlreadyFunded < c.Amount) || c.TargetDate.Value.Year > 9900))
                throw new ArgumentException("Choose a target date on or after contributions start.");
            if(c.Mode != "Monthly" && c.TargetDate > plan.StartDate.AddYears(10)) throw new ArgumentException("Choose a target within the next 10 years.");
            if (c.Mode == "Recurring" && c.RepeatMonths is not (1 or 3 or 6 or 12)) throw new ArgumentException("Choose a monthly, quarterly, six-month, or annual repeat.");
        }
    }
    public static bool Equivalent(SavingsPlan? a,SavingsPlan b) => a is not null && a.StartDate==b.StartDate && a.Components.Count==b.Components.Count &&
        a.Components.Zip(b.Components).All(p=>p.First.Key==p.Second.Key && p.First.Name==p.Second.Name && p.First.Mode==p.Second.Mode && p.First.Amount==p.Second.Amount && p.First.TargetDate==p.Second.TargetDate && p.First.RepeatMonths==p.Second.RepeatMonths && p.First.AlreadyFunded==p.Second.AlreadyFunded);
    public static void Money(decimal amount)
    {
        if (amount < 0 || amount > 999_999_999m || decimal.Round(amount,2) != amount) throw new ArgumentException("Use an amount from zero to 999,999,999 with at most two decimal places.");
    }
    private static decimal Round(decimal n) => decimal.Round(n,2,MidpointRounding.AwayFromZero);
    public static decimal Between(SavingsPlan plan, SavingsComponent c, DateOnly from, DateOnly to)
    {
        if (to < plan.StartDate || to < from) return 0;
        from = from < plan.StartDate ? plan.StartDate : from;
        if (c.Mode == "Monthly")
        {
            decimal amount = 0;
            for (var month = new DateOnly(from.Year,from.Month,1); month <= to; month = month.AddMonths(1))
            {
                var days = DateTime.DaysInMonth(month.Year,month.Month);
                var first = from > month ? from.Day : 1;
                var last = to.Year == month.Year && to.Month == month.Month ? to.Day : days;
                amount += Round(c.Amount*last/days)-Round(c.Amount*(first-1)/days);
            }
            return amount;
        }
        decimal result = 0; var start = plan.StartDate; var end = c.TargetDate!.Value; var needed = Math.Max(0,c.Amount-c.AlreadyFunded);
        for (var cycle=0; cycle<1200 && start<=to; cycle++)
        {
            var a = from > start ? from : start; var b = to < end ? to : end;
            if (a<=b) { var days=end.DayNumber-start.DayNumber+1; result += Round(needed*(b.DayNumber-start.DayNumber+1)/days)-Round(needed*(a.DayNumber-start.DayNumber)/days); }
            if (c.Mode == "Target" || end >= to) break;
            start=end.AddDays(1); end=c.TargetDate.Value.AddMonths((cycle+1)*c.RepeatMonths); needed=c.Amount;
        }
        return result;
    }
    public static DateOnly ContributionDate(SavingsPlan plan,DateOnly start,DateOnly end)
    {
        var due=end;
        foreach(var c in plan.Components.Where(c=>c.Mode!="Monthly" && c.TargetDate.HasValue))
        {
            var date=c.TargetDate!.Value;
            if(c.Mode=="Recurring") while(date<start && date.Year<9900)date=date.AddMonths(c.RepeatMonths);
            if(date>=start && date<due)due=date;
        }
        return due;
    }
    public static decimal Between(SavingsPlan plan, DateOnly from, DateOnly to) => plan.Components.Sum(c => Between(plan,c,from,to));
    public static decimal MonthlyEquivalent(SavingsPlan plan) => Round(plan.Components.Sum(c => c.Mode == "Monthly" ? c.Amount :
        c.Mode == "Recurring" ? c.Amount/c.RepeatMonths : Math.Max(0,c.Amount-c.AlreadyFunded)*365.2425m/12/(Math.Max(1,c.TargetDate!.Value.DayNumber-plan.StartDate.DayNumber+1))));
}
