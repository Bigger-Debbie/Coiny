import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetCalendar } from './budget-calendar';
import { BudgetCalendarStub, BudgetActualsStub } from './budget-calendar.stub';
import { BudgetActualsPanel } from './budget-actuals';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { BudgetDashboard } from './budget-dashboard';
import { DonutChart } from '../../shared/components/donut-chart/donut-chart';
import { BudgetMonth, BudgetIncomePeriod, AllocationType } from '../../shared/models/budget/budget';

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
  beforeEach(() => { TestBed.configureTestingModule({ providers:[provideHttpClient(),provideHttpClientTesting()] }); TestBed.overrideComponent(BudgetDashboard, { remove: { imports: [BudgetCalendar, BudgetActualsPanel] }, add: { imports: [BudgetCalendarStub, BudgetActualsStub] } }); });
  function render(month: BudgetMonth | null = plan) {
    const f = TestBed.createComponent(BudgetDashboard); f.componentInstance.mode.set('month');
    f.componentRef.setInput('month', month); f.componentRef.setInput('selectedMonth', '2026-10-01');
    f.detectChanges(); return f;
  }
  it('defaults to one shared period for totals, breakdown, actuals, and calendar', () => {
    const f = TestBed.createComponent(BudgetDashboard); const http = TestBed.inject(HttpTestingController);
    f.componentRef.setInput('month',plan); f.componentRef.setInput('selectedMonth','2026-10-01'); f.detectChanges();
    const projected = { ...plan, expectedIncome:2500, allocated:2700, unassigned:0, overBudget:200 };
    const period: BudgetIncomePeriod = { asOf:'2026-10-30',startDate:'2026-10-30',endDate:'2026-11-12',nextPayday:'2026-11-13',sourceKey:null,sources:[],bills:[],contributions:[],billsTotal:0,contributionsTotal:0,plan:projected };
    http.expectOne(r=>r.url.endsWith('/income-period')).flush(period); f.detectChanges();
    expect(f.componentInstance.mode()).toBe('period'); expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('-$200.00');
    expect(f.debugElement.query(By.directive(BudgetActualsStub)).componentInstance.periodAsOf).toBe('2026-10-30');
    expect(f.debugElement.query(By.directive(BudgetCalendarStub)).componentInstance.controlledPeriod).toEqual(period);
    f.componentInstance.navigatePeriod(1); const next=http.expectOne(r=>r.url.endsWith('/income-period'));
    expect(next.request.params.get('asOf')).toBe('2026-11-13'); next.flush({ ...period,asOf:'2026-11-13',startDate:'2026-11-13',endDate:'2026-11-26' }); f.detectChanges();
    f.componentInstance.selectMode('month'); f.detectChanges(); expect(f.componentInstance.displayPlan).toEqual(plan); http.verify();
  });
  it('uses fixed unique budget colors with green reserved for the remainder', () => {
    const types: AllocationType[]=['Housing','Bills','Subscriptions','Spending','Savings','Investments'];
    const f=render({ ...plan,expectedIncome:1000,allocated:600,unassigned:400,allocations:types.map((type,i)=>({id:i+1,name:type,allocationType:type,plannedAmount:100,occurrenceCount:1})) });
    const chart=f.debugElement.query(By.directive(DonutChart)).componentInstance as DonutChart;
    expect(new Set(chart.slices().map(c=>c.color)).size).toBe(7);
    expect(chart.slices().find(c=>c.categoryName==='Monthly remainder')?.color).toBe('#047c04');
    expect(chart.slices().find(c=>c.categoryName==='Subscriptions')?.color).toBe('#486c9b');
  });
  it('groups allocations, includes unassigned income, and excludes payroll deductions from the donut', () => {
    const f = render(); const c = f.componentInstance;
    expect(c.categories.map(i => [i.categoryName, i.amount])).toEqual([['Housing', 2000], ['Bills', 1500], ['Monthly remainder', 1500]]);
    expect(c.categories.reduce((sum, i) => sum + i.percentage, 0)).toBe(100);
    const chart = f.debugElement.query(By.directive(DonutChart)).componentInstance as DonutChart;
    const potential = chart.slices().find(slice => slice.categoryName === 'Monthly remainder')!;
    expect(potential.color).toBe('#047c04'); expect(potential.strokeWidth).toBe(16);
    expect(potential.percentage).toBe(30);
    expect(f.nativeElement.querySelectorAll('.slice-shape circle')[2].getAttribute('stroke-width')).toBe('16');
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
    expect(f.componentInstance.categories.map(i => i.categoryName)).toEqual(['Monthly remainder']);
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
