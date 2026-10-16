using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

internal static class BudgetScheduleMapping
{
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, BudgetSchedule> builder)
        where TOwner : class
    {
        builder.Property(s => s.Frequency).HasColumnName("ScheduleFrequency");
        builder.Property(s => s.StartDate).HasColumnName("ScheduleStartDate");
        builder.Property(s => s.EndDate).HasColumnName("ScheduleEndDate");
        builder.Property(s => s.DayOfMonth).HasColumnName("ScheduleDayOfMonth");
        builder.Property(s => s.SecondDayOfMonth).HasColumnName("ScheduleSecondDayOfMonth");
    }

    public static string Constraint(bool optional)
    {
        const string valid = """
            "ScheduleFrequency" IS NOT NULL AND "ScheduleStartDate" IS NOT NULL
            AND ("ScheduleEndDate" IS NULL OR "ScheduleEndDate" >= "ScheduleStartDate")
            AND (
                ("ScheduleFrequency" IN (0, 1) AND "ScheduleDayOfMonth" IS NULL AND "ScheduleSecondDayOfMonth" IS NULL)
                OR ("ScheduleFrequency" = 2 AND "ScheduleDayOfMonth" BETWEEN 1 AND 31
                    AND "ScheduleSecondDayOfMonth" > "ScheduleDayOfMonth" AND "ScheduleSecondDayOfMonth" <= 31)
                OR ("ScheduleFrequency" = 3 AND "ScheduleDayOfMonth" BETWEEN 1 AND 31 AND "ScheduleSecondDayOfMonth" IS NULL)
            )
            """;
        const string absent = """
            "ScheduleFrequency" IS NULL AND "ScheduleStartDate" IS NULL AND "ScheduleEndDate" IS NULL
            AND "ScheduleDayOfMonth" IS NULL AND "ScheduleSecondDayOfMonth" IS NULL
            """;
        return optional ? $"({absent}) OR COALESCE(({valid}), 0)" : $"COALESCE(({valid}), 0)";
    }
}
