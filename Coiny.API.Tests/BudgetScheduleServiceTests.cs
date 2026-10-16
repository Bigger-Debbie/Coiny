using Coiny.API.Enums;
using Coiny.API.Models;
using Coiny.API.Services;

namespace Coiny.API.Tests;

public class BudgetScheduleServiceTests
{
    private readonly BudgetScheduleService service = new();

    [Fact]
    public void SixMonthlyAndYearlyUseOriginalAnchorAcrossShortMonthsAndYears()
    {
        var sixMonthly = new BudgetSchedule { Frequency = BudgetFrequency.EverySixMonths, StartDate = new(2026,8,31) };
        Assert.Empty(service.GetOccurrences(sixMonthly, new(2026,7,1)));
        Assert.Equal(new DateOnly(2027,2,28), Assert.Single(service.GetOccurrences(sixMonthly, new(2027,2,1))));
        Assert.Empty(service.GetOccurrences(sixMonthly, new(2027,3,1)));
        Assert.Equal(new DateOnly(2027,8,31), Assert.Single(service.GetOccurrences(sixMonthly, new(2027,8,1))));
        sixMonthly.EndDate = new(2027,2,27);
        Assert.Empty(service.GetOccurrences(sixMonthly, new(2027,2,1)));
        var yearly = new BudgetSchedule { Frequency = BudgetFrequency.Yearly, StartDate = new(2024,2,29) };
        Assert.Equal(new DateOnly(2025,2,28), Assert.Single(service.GetOccurrences(yearly, new(2025,2,1))));
        Assert.Equal(new DateOnly(2028,2,29), Assert.Single(service.GetOccurrences(yearly, new(2028,2,1))));
        Assert.Empty(service.GetOccurrences(yearly, new(2028,3,1)));
        yearly.DayOfMonth = 15;
        Assert.Throws<ArgumentException>(() => service.Validate(yearly));
        yearly.DayOfMonth = null; yearly.StartDate = DateOnly.MaxValue;
        Assert.Equal(DateOnly.MaxValue, Assert.Single(service.GetOccurrences(yearly, new(9999,12,1))));
    }

    [Theory]
    [InlineData(2026, 10, 2, 16, 30)]
    [InlineData(2027, 1, 8, 22, 0)]
    public void EveryTwoWeeksUsesAnchorInsteadOfRestartingEachMonth(int year, int month, int first, int second, int third)
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026, 10, 2) };
        var expected = third == 0 ? new[] { first, second } : [first, second, third];
        Assert.Equal(expected, service.GetOccurrences(schedule, new(year, month, 1)).Select(d => d.Day));
    }

    [Fact]
    public void WeeklyIncludesBoundariesAndKeepsWeekendDates()
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.Weekly, StartDate = new(2026, 10, 3), EndDate = new(2026, 10, 17) };
        Assert.Equal(new[] { 3, 10, 17 }, service.GetOccurrences(schedule, new(2026,10,1)).Select(d => d.Day));
        Assert.Empty(service.GetOccurrences(schedule, new(2026,9,1)));
        Assert.Empty(service.GetOccurrences(schedule, new(2026,11,1)));
    }

    [Theory]
    [InlineData(2027, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    public void MonthlyClampsWithoutChangingTheDefinition(int year, int month, int expectedDay)
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,1,1), DayOfMonth = 31 };
        Assert.Equal(expectedDay, Assert.Single(service.GetOccurrences(schedule, new(year,month,1))).Day);
        Assert.Equal(31, schedule.DayOfMonth);
        Assert.Equal(31, Assert.Single(service.GetOccurrences(schedule, new(2028,3,1))).Day);
    }

    [Fact]
    public void TwiceMonthlyRetainsBothPaySlotsWhenTheyClampToOneDate()
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.TwiceMonthly, StartDate = new(2026,1,1), DayOfMonth = 30, SecondDayOfMonth = 31 };
        Assert.Equal(new[] { new DateOnly(2027,2,28), new DateOnly(2027,2,28) }, service.GetOccurrences(schedule, new(2027,2,1)));
        schedule.DayOfMonth = 15;
        Assert.Equal(new[] { 15, 28 }, service.GetOccurrences(schedule, new(2027,2,1)).Select(d => d.Day));
    }

    [Fact]
    public void MonthlyBoundsCanExcludeOneSlotOrTheWholeMonth()
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.TwiceMonthly, StartDate = new(2026,10,20), EndDate = new(2026,11,15), DayOfMonth = 15, SecondDayOfMonth = 31 };
        Assert.Equal(31, Assert.Single(service.GetOccurrences(schedule, new(2026,10,1))).Day);
        Assert.Equal(15, Assert.Single(service.GetOccurrences(schedule, new(2026,11,1))).Day);
        Assert.Empty(service.GetOccurrences(schedule, new(2026,12,1)));
    }

    [Fact]
    public void MaximumCalendarYearDoesNotOverflow()
    {
        var schedule = new BudgetSchedule { Frequency = BudgetFrequency.Weekly, StartDate = new(9999,12,1) };
        Assert.Equal(new[] { 1, 8, 15, 22, 29 }, service.GetOccurrences(schedule, new(9999,12,1)).Select(d => d.Day));
        schedule.Frequency = BudgetFrequency.Monthly; schedule.DayOfMonth = 31;
        Assert.Equal(DateOnly.MaxValue, Assert.Single(service.GetOccurrences(schedule, new(9999,12,1))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void InvalidDefinitionsAreRejected(int scenario)
    {
        var s = new BudgetSchedule { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 15 };
        switch (scenario)
        {
            case 0: s.StartDate = default; break;
            case 1: s.EndDate = new(2026,9,30); break;
            case 2: s.DayOfMonth = 32; break;
            case 3: s.Frequency = BudgetFrequency.Weekly; break;
            case 4: s.Frequency = BudgetFrequency.TwiceMonthly; s.SecondDayOfMonth = 15; break;
            case 5: s.Frequency = (BudgetFrequency)99; break;
        }
        Assert.Throws<ArgumentException>(() => service.GetOccurrences(s, new(2026,10,1)));
    }
}
