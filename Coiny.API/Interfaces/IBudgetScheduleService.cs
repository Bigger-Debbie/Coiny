using Coiny.API.Models;

namespace Coiny.API.Interfaces;

public interface IBudgetScheduleService
{
    void Validate(BudgetSchedule schedule);
    IReadOnlyList<DateOnly> GetOccurrences(BudgetSchedule schedule, DateOnly month);
}
