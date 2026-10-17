import { BudgetCalendar } from './budget-calendar';
import { BudgetCalendarStub } from './budget-calendar.stub';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { BudgetDashboard } from './budget-dashboard';
import { DonutChart } from '../../shared/components/donut-chart/donut-chart';
import { BudgetMonth } from '../../shared/models/budget/budget';

const plan: BudgetMonth = {
  month: '2026-10-01', revisionId: 3, expectedIncome: 5000, allocated: 3500, unassigned: 1500, overBudget: 0,
  healthInsurance: 200, retirementContributions: 400, events: [],
  incomeSources: [{ id: 1, name: 'Salary', allocationType: null, plannedAmount: 5000, occurrenceCount: 2 }],
  allocations: [
    { id: 1, name: 'Mortgage', allocationType: 'Housing', plannedAmount: 2000, occurrenceCount: 1 },
    { id: 2, name: 'Electric', allocationType: 'Bills', plannedAmount: 1000, occurrenceCount: 1 },
    { id: 3, name: 'Water', allocationType: 'Bills', plannedAmount: 500, occurrenceCount: 1 },
  ],
};
describe('BudgetDashboard', () => {
  beforeEach(() => TestBed.overrideComponent(BudgetDashboard, { remove: { imports: [BudgetCalendar] }, add: { imports: [BudgetCalendarStub] } }));
  function render(month: BudgetMonth | null = plan) {
    const f = TestBed.createComponent(BudgetDashboard);
    f.componentRef.setInput('month', month); f.componentRef.setInput('selectedMonth', '2026-10-01');
    f.detectChanges(); return f;
  }
  it('groups allocations, includes unassigned income, and excludes payroll deductions from the donut', () => {
    const f = render(); const c = f.componentInstance;
    expect(c.categories.map(i => [i.categoryName, i.amount])).toEqual([['Housing', 2000], ['Bills', 1500], ['Unassigned', 1500]]);
    expect(c.categories.reduce((sum, i) => sum + i.percentage, 0)).toBe(100);
    const chart = f.debugElement.query(By.directive(DonutChart)).componentInstance as DonutChart;
    expect(chart.total).toBe(5000); expect(chart.centerLabel).toBe('Monthly income');
    expect(chart.showCounts).toBe(false); expect(chart.unsigned).toBe(true);
    expect(f.nativeElement.textContent).toContain('Already deducted from paychecks');
    expect(f.nativeElement.querySelector('app-donut-chart').textContent).not.toContain('transactions');
    expect(chart.description(chart.slices()[0])).not.toContain('transactions');
  });
  it('uses planned allocation shares when over budget without inventing an income or shortfall slice', () => {
    const f = render({ ...plan, expectedIncome: 2000, allocated: 3500, unassigned: 0, overBudget: 1500 });
    expect(f.componentInstance.categories.map(i => i.categoryName)).toEqual(['Housing', 'Bills']);
    expect(f.componentInstance.total).toBe(3500);
    expect(f.nativeElement.textContent).toContain('exceeds take-home income by $1,500.00');
    expect(f.nativeElement.querySelector('.center').textContent).toContain('Total planned');
  });
  it('handles all-unassigned, zero income, and months with no applicable revision', () => {
    const f = render({ ...plan, allocated: 0, unassigned: 5000, allocations: [] });
    expect(f.componentInstance.categories.map(i => i.categoryName)).toEqual(['Unassigned']);
    f.componentRef.setInput('month', { ...plan, expectedIncome: 0, allocated: 0, unassigned: 0, allocations: [] }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('No income or allocations');
    expect(f.debugElement.query(By.directive(DonutChart))).toBeNull();
    f.componentRef.setInput('month', { ...plan, revisionId: null }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('No published plan for this month');
  });
  it('supports month changes and year boundaries, with bounded navigation and retry', () => {
    const f = render(); const emit = vi.spyOn(f.componentInstance.selectMonth, 'emit');
    f.componentRef.setInput('selectedMonth', '2026-12-01'); f.detectChanges();
    f.nativeElement.querySelector('[aria-label="Next month"]').click(); expect(emit).toHaveBeenLastCalledWith('2027-01-01');
    f.componentInstance.change('2027-02'); expect(emit).toHaveBeenLastCalledWith('2027-02-01');
    emit.mockClear(); f.componentInstance.change('bad'); f.componentInstance.change('2027-13'); expect(emit).not.toHaveBeenCalled();
    f.componentRef.setInput('selectedMonth', '9999-12-01'); f.detectChanges();
    expect(f.nativeElement.querySelector('[aria-label="Next month"]').disabled).toBe(true);
    f.componentRef.setInput('error', true); f.detectChanges();
    f.nativeElement.querySelector('.button-outline').click(); expect(emit).toHaveBeenLastCalledWith('9999-12-01');
    f.componentRef.setInput('loading', true); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Loading the published plan');
    expect(f.nativeElement.querySelector('app-donut-chart')).toBeNull();
  });
});
