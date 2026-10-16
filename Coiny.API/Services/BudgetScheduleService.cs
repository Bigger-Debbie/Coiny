using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;

namespace Coiny.API.Services;

public class BudgetScheduleService : IBudgetScheduleService
{
    public void Validate(BudgetSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!Enum.IsDefined(schedule.Frequency) || schedule.StartDate == DateOnly.MinValue)
            throw new ArgumentException("A supported frequency and start date are required.");
        if (schedule.EndDate < schedule.StartDate)
            throw new ArgumentException("Schedule end date cannot precede its start date.");
        if (schedule.Frequency is BudgetFrequency.Weekly or BudgetFrequency.EveryTwoWeeks or BudgetFrequency.EverySixMonths or BudgetFrequency.Yearly)
        {
            if (schedule.DayOfMonth.HasValue || schedule.SecondDayOfMonth.HasValue)
                throw new ArgumentException("This frequency repeats from the start date and does not use monthly day numbers.");
        }
        else
        {
            if (schedule.DayOfMonth is null or < 1 or > 31)
                throw new ArgumentException("A monthly day from 1 to 31 is required.");
            if (schedule.Frequency == BudgetFrequency.TwiceMonthly)
            {
                if (!schedule.SecondDayOfMonth.HasValue || schedule.SecondDayOfMonth <= schedule.DayOfMonth || schedule.SecondDayOfMonth > 31)
                    throw new ArgumentException("The second monthly day must be later than the first and at most 31.");
            }
            else if (schedule.SecondDayOfMonth.HasValue)
                throw new ArgumentException("A monthly schedule has only one day number.");
        }
    }

    public IReadOnlyList<DateOnly> GetOccurrences(BudgetSchedule schedule, DateOnly month)
    {
        Validate(schedule);
        if (month.Day != 1) throw new ArgumentException("Month must be the first day of a calendar month.");
        var monthEnd = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        var start = schedule.StartDate > month ? schedule.StartDate : month;
        var end = schedule.EndDate.HasValue && schedule.EndDate < monthEnd ? schedule.EndDate.Value : monthEnd;
        if (start > end) return Array.Empty<DateOnly>();
        var dates = new List<DateOnly>();
        if (schedule.Frequency is BudgetFrequency.Weekly or BudgetFrequency.EveryTwoWeeks)
        {
            var interval = schedule.Frequency == BudgetFrequency.Weekly ? 7 : 14;
            var remainder = (start.DayNumber - schedule.StartDate.DayNumber) % interval;
            var first = start.DayNumber + (remainder == 0 ? 0 : interval - remainder);
            for (var day = first; day <= end.DayNumber; day += interval)
                dates.Add(DateOnly.FromDayNumber(day));
        }
        else if (schedule.Frequency is BudgetFrequency.EverySixMonths or BudgetFrequency.Yearly)
        {
            var elapsedMonths = (month.Year - schedule.StartDate.Year) * 12 + month.Month - schedule.StartDate.Month;
            var interval = schedule.Frequency == BudgetFrequency.EverySixMonths ? 6 : 12;
            if (elapsedMonths >= 0 && elapsedMonths % interval == 0) AddMonthlyDay(schedule.StartDate.Day);
        }
        else
        {
            AddMonthlyDay(schedule.DayOfMonth!.Value);
            if (schedule.Frequency == BudgetFrequency.TwiceMonthly)
                AddMonthlyDay(schedule.SecondDayOfMonth!.Value);
        }
        return dates.AsReadOnly();

        void AddMonthlyDay(int requestedDay)
        {
            var date = new DateOnly(month.Year, month.Month, Math.Min(requestedDay, monthEnd.Day));
            // Each twice-monthly slot is retained, even if both clamp to the same day.
            if (date >= start && date <= end) dates.Add(date);
        }
    }
}
