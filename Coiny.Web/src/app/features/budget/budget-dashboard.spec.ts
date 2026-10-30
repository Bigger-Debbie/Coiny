import { SavingsFunds } from '../savings/savings-funds';
import { AuthService } from '../../core/services/auth.service';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetCalendar } from './budget-calendar';
import { BudgetCalendarStub, BudgetActualsStub, SavingsFundsStub } from './budget-calendar.stub';
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
  beforeEach(() => { TestBed.configureTestingModule({ providers:[provideHttpClient(),provideHttpClientTesting()] }); TestBed.overrideComponent(BudgetDashboard, { remove: { imports: [BudgetCalendar, BudgetActualsPanel, SavingsFunds] }, add: { imports: [BudgetCalendarStub, BudgetActualsStub, SavingsFundsStub] } }); });
  function render(month: BudgetMonth | null = plan) {
    const f = TestBed.createComponent(BudgetDashboard); f.componentInstance.mode.set('month');
    f.componentRef.setInput('month', month); f.componentRef.setInput('selectedMonth', '2026-10-01');
    f.detectChanges(); return f;
  }
  it('defaults to one shared period for totals, breakdown, actuals, and calendar', () => {
    const f = TestBed.createComponent(BudgetDashboard); const http = TestBed.inject(HttpTestingController);
    f.componentRef.setInput('month',plan); f.componentRef.setInput('selectedMonth','2026-10-01'); f.detectChanges();
    const projected = { ...plan, expectedIncome:2500, allocated:2700, unassigned:0, overBudget:200 };
    const period: BudgetIncomePeriod = { asOf:'2026-10-30',startDate:'2026-10-30',endDate:'2026-11-12',nextPayday:'2026-11-13',sourceKey:null,sources:[],bills:[],contributions:[],billsTotal:0,contributionsTotal:0,awaitingTransactionTotal:500,plan:projected };
    http.expectOne(r=>r.url.endsWith('/income-period')).flush(period);
    const forecast = http.expectOne(r=>r.url.endsWith('/forecast'));
    expect(forecast.request.params.get('months')).toBe('0'); expect(forecast.request.params.get('asOf')).toBe('2026-10-30');
    forecast.flush({ points:[{ date:'2026-11-12',planned:-200,actual:null,forecast:-350 }] }); f.detectChanges();
    expect(f.componentInstance.mode()).toBe('period'); expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('-$350.00');
    expect(f.nativeElement.querySelector('.remaining-total').textContent).toContain('Planned remainder -$200.00');
    expect(f.nativeElement.querySelector('.remaining-total').textContent).toContain('$500.00 awaiting transaction links');
    const help = f.nativeElement.querySelector('.reserve-help') as HTMLDetailsElement;
    expect(help.open).toBe(false); help.querySelector('summary')!.click(); expect(help.open).toBe(true);
    expect(help.textContent).toContain('Their transactions may already count as spending.');
    help.dispatchEvent(new KeyboardEvent('keydown', { key:'Escape', bubbles:true })); expect(help.open).toBe(false);
    help.querySelector('summary')!.click(); f.nativeElement.querySelector('.income-total').click(); expect(help.open).toBe(false);
    // Closing help must not cancel the browser's checkbox activation elsewhere on the page.
    const checkbox = document.createElement('input'); checkbox.type = 'checkbox'; f.nativeElement.append(checkbox);
    help.querySelector('summary')!.click(); checkbox.click(); expect(checkbox.checked).toBe(true); expect(help.open).toBe(false);
    checkbox.click(); expect(checkbox.checked).toBe(false);
    expect(f.nativeElement.querySelector('.remaining-total').classList.contains('over')).toBe(true);
    expect(f.debugElement.query(By.directive(BudgetActualsStub)).componentInstance.periodAsOf).toBe('2026-10-30');
    expect(f.debugElement.query(By.directive(BudgetCalendarStub)).componentInstance.controlledPeriod).toEqual(period);
    const incomeCard = f.nativeElement.querySelector('.income-total');
    const savingsPanel = f.debugElement.query(By.directive(SavingsFundsStub)).componentInstance;
    f.componentInstance.navigatePeriod(1); f.detectChanges();
    expect(f.nativeElement.querySelector('.income-total')).toBe(incomeCard);
    expect(f.debugElement.query(By.directive(SavingsFundsStub)).componentInstance).toBe(savingsPanel);
    expect(f.nativeElement.querySelector('.budget-content').getAttribute('aria-busy')).toBe('true');
    expect(f.nativeElement.querySelector('.budget-content').hasAttribute('inert')).toBe(true);
    const next=http.expectOne(r=>r.url.endsWith('/income-period'));
    expect(next.request.params.get('asOf')).toBe('2026-11-13'); next.flush({ ...period,asOf:'2026-11-13',startDate:'2026-11-13',endDate:'2026-11-26' }); f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/forecast')).flush({ points:[{ date:'2026-11-26',planned:500,actual:450,forecast:null }] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('$450.00');
    expect(f.nativeElement.querySelector('.income-total')).toBe(incomeCard);
    expect(f.nativeElement.querySelector('.budget-content').hasAttribute('inert')).toBe(false);
    expect(f.nativeElement.querySelector('.budget-content').classList.contains('is-updating')).toBe(false);
    f.debugElement.query(By.directive(BudgetActualsStub)).componentInstance.refreshed.emit();
    http.expectOne(r=>r.url.endsWith('/forecast')).flush({ points:[{ date:'2026-11-26',planned:500,actual:400,forecast:null }] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('$400.00');
    f.componentInstance.loadRemainder(); http.expectOne(r=>r.url.endsWith('/forecast')).flush({}, { status:500,statusText:'Error' }); f.detectChanges();
    expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('—');
    expect(f.nativeElement.querySelector('.remaining-total').textContent).toContain('Forecast couldn’t load');
    f.componentInstance.selectMode('month'); f.detectChanges(); expect(f.componentInstance.displayPlan).toEqual(plan); expect(f.nativeElement.querySelector('.unlinked-reserve')).toBeNull(); http.verify();
  });
  it('keeps monthly cards and child panels mounted while the next month loads', () => {
    const f = render();
    const card = f.nativeElement.querySelector('.income-total');
    const calendar = f.debugElement.query(By.directive(BudgetCalendarStub)).componentInstance;
    f.componentRef.setInput('selectedMonth', '2026-11-01');
    f.componentRef.setInput('loading', true); f.detectChanges();
    expect(f.nativeElement.querySelector('.income-total')).toBe(card);
    expect(f.debugElement.query(By.directive(BudgetCalendarStub)).componentInstance).toBe(calendar);
    expect(f.nativeElement.querySelector('.budget-content').getAttribute('aria-busy')).toBe('true');
    expect(f.nativeElement.querySelector('.budget-content').hasAttribute('inert')).toBe(true);
    f.componentRef.setInput('month', {...plan, month:'2026-11-01', expectedIncome:6000});
    f.componentRef.setInput('loading', false); f.detectChanges();
    expect(f.nativeElement.querySelector('.income-total')).toBe(card);
    expect(card.textContent).toContain('$6,000.00');
    expect(f.nativeElement.querySelector('.budget-content').hasAttribute('inert')).toBe(false);
  });
  it('restores the chosen outlook and accounts for this user and household', () => {
    const key='coiny.cash-outlook.cash-user.3';
    TestBed.overrideProvider(AuthService,{useValue:{currentUser:()=>({id:'cash-user'})}});
    const storage=new Map<string,string>();
    vi.stubGlobal('localStorage',{getItem:(key:string)=>storage.get(key) ?? null,setItem:(key:string,value:string)=>storage.set(key,value),removeItem:(key:string)=>storage.delete(key)});
    localStorage.setItem(key,JSON.stringify({view:'cash',accountIds:[1,3]}));
    try {
      const f=TestBed.createComponent(BudgetDashboard); const http=TestBed.inject(HttpTestingController);
      f.componentRef.setInput('month',plan); f.componentRef.setInput('selectedMonth','2026-10-01'); f.detectChanges();
      http.expectOne(r=>r.url.endsWith('/income-period')).flush({householdId:3,asOf:'2026-10-26',startDate:'2026-10-16',endDate:'2026-10-29',nextPayday:'2026-10-30',sourceKey:null,sources:[],bills:[],contributions:[],billsTotal:0,contributionsTotal:0,plan});
      http.expectOne(r=>r.url.endsWith('/forecast')).flush({points:[{date:'2026-10-29',forecast:488.39}]}); f.detectChanges();
      const cash=http.expectOne(r=>r.url.endsWith('/available-cash')); expect(cash.request.body.accountIds).toEqual([1,3]);
      cash.flush({balanceDate:'2026-10-26',endDate:'2026-10-29',cashBalance:500,cardBalance:100,expectedIncome:0,remainingAllocations:100,expectedRemaining:300,accountIds:[1,3],accounts:[]}); f.detectChanges();
      expect(f.nativeElement.querySelector('app-available-cash').textContent).toContain('$300.00');
      f.componentInstance.selectOutlook('surplus'); f.changeDetectorRef.markForCheck(); f.detectChanges();
      expect(f.nativeElement.querySelector('.remaining-total strong').textContent).toContain('$488.39');
      expect(JSON.parse(localStorage.getItem(key)!).view).toBe('surplus'); http.verify();
    } finally { vi.unstubAllGlobals(); }
  });
  it('uses fixed unique budget colors with green reserved for the remainder', () => {
    const types: AllocationType[]=['Housing','Bills','Subscriptions','Spending','Savings','Investments'];
    const f=render({ ...plan,expectedIncome:1000,allocated:600,unassigned:400,allocations:types.map((type,i)=>({id:i+1,name:type,allocationType:type,plannedAmount:100,occurrenceCount:1})) });
    const chart=f.debugElement.query(By.directive(DonutChart)).componentInstance as DonutChart;
    expect(new Set(chart.slices().map(c=>c.color)).size).toBe(7);
    expect(chart.slices().find(c=>c.categoryName==='Monthly remainder')?.color).toBe('#047c04');
    expect(chart.slices().find(c=>c.categoryName==='Subscriptions')?.color).toBe('#486c9b');
  });
  it('opens item management from breakdown rows and section add actions and refreshes after saving', () => {
    const f=render({ ...plan,incomeSources:[{ ...plan.incomeSources[0],sourceKey:'salary' }] });
    f.nativeElement.querySelector('.editable-item').click();
    expect(f.componentInstance.itemEditor()?.selection).toEqual({ isIncome:true,sourceKey:'salary' });
    f.componentInstance.itemEditor.set(null);
    f.nativeElement.querySelector('.add-item').click();
    expect(f.componentInstance.itemEditor()?.selection).toMatchObject({ isIncome:true,add:true });
    const changed=vi.spyOn(f.componentInstance.budgetChanged,'emit'); f.componentInstance.itemSaved();
    expect(changed).toHaveBeenCalledOnce(); expect(f.componentInstance.itemEditor()).toBeNull();
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
