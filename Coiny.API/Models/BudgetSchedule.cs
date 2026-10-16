using Coiny.API.Enums;

namespace Coiny.API.Models;

/// <summary>
/// A recurrence definition owned by a single income source or allocation.
/// StartDate anchors weekly/fortnightly schedules and bounds all schedules.
/// Monthly day numbers remain unchanged when an occurrence is clamped to a short month.
/// </summary>
public class BudgetSchedule
{
    public BudgetFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DayOfMonth { get; set; }
    public int? SecondDayOfMonth { get; set; }
}
