using Coiny.API.DTOs.Savings;
using Coiny.API.Services;
namespace Coiny.API.Tests;
public class SavingsGoalTests
{
    [Fact] public void TargetUsesExistingCashAndPreservesCentsAcrossIrregularPeriods()
    {
        var c=new SavingsComponent{Name="Vacation",Amount=3000,AlreadyFunded=1000,TargetDate=new(2027,6,1)};
        var p=new SavingsPlan{StartDate=new(2026,10,27),Components=[c]};SavingsGoalCalculator.Validate(p);
        decimal sum=0;for(var day=p.StartDate;day<=c.TargetDate;day=day.AddDays(1))sum+=SavingsGoalCalculator.Between(p,c,day,day);
        Assert.Equal(2000,sum);Assert.Equal(0,SavingsGoalCalculator.Between(p,c,new(2027,6,2),new(2027,7,1)));
        Assert.Equal(0,SavingsGoalCalculator.Between(p,c,new(2026,1,1),new(2026,10,26)));
    }
    [Fact] public void RecurringCostCatchesUpThenFundsFullNextCycle()
    {
        var c=new SavingsComponent{Name="Tires",Mode="Recurring",Amount=1000,AlreadyFunded=400,TargetDate=new(2027,1,31),RepeatMonths=12};
        var p=new SavingsPlan{StartDate=new(2026,11,1),Components=[c]};
        Assert.Equal(600,SavingsGoalCalculator.Between(p,c,p.StartDate,c.TargetDate.Value));
        Assert.Equal(1000,SavingsGoalCalculator.Between(p,c,new(2027,2,1),new(2028,1,31)));
        Assert.Equal(83.33m,SavingsGoalCalculator.MonthlyEquivalent(p));
    }
    [Fact] public void MonthlyReserveKeepsExactAmountAcrossFebruaryAndCrossMonthPeriods()
    {
        var c=new SavingsComponent{Name="Repairs",Mode="Monthly",Amount=100};var p=new SavingsPlan{StartDate=new(2028,1,1),Components=[c]};
        var first=SavingsGoalCalculator.Between(p,c,new(2028,2,1),new(2028,2,14));
        var second=SavingsGoalCalculator.Between(p,c,new(2028,2,15),new(2028,2,29));
        Assert.Equal(100,first+second);
        Assert.Equal(200,SavingsGoalCalculator.Between(p,c,new(2028,2,1),new(2028,3,31)));
    }
    [Theory][InlineData("Unsupported",100)][InlineData("Target",-1)][InlineData("Target",1.001)]
    public void InvalidDefinitionsAreRejected(string mode,decimal amount)
    {
        Assert.Throws<ArgumentException>(()=>SavingsGoalCalculator.Validate(new(){StartDate=new(2026,10,1),Components=[new(){Name="Goal",Mode=mode,Amount=amount,TargetDate=new(2026,11,1)}]}));
    }
}
