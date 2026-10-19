import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetCalendar } from './budget-calendar';
import { BudgetIncomePeriod, BudgetMonth } from '../../shared/models/budget/budget';

const bill = { date: '2026-10-17', itemId: 1, name: 'Electric', isIncome: false, allocationType: 'Bills' as const, amount: 100 };
const period: BudgetIncomePeriod = { asOf: '2026-10-16', sourceKey: null,
  sources: [{ sourceKey: 'salary-a', name: 'Salary A' }], startDate: '2026-10-16', endDate: '2026-10-22', nextPayday: '2026-10-23',
  billsTotal: 100, contributionsTotal: 0, bills: [bill], contributions: [] };
const month: BudgetMonth = { month: '2026-10-01', revisionId: 1, expectedIncome: 2000, allocated: 100, unassigned: 1900, overBudget: 0,
  healthInsurance: 0, retirementContributions: 0, incomeSources: [], allocations: [], events: [bill,
    { date: '2026-10-16', itemId: 2, name: 'Salary A', isIncome: true, allocationType: null, amount: 2000 }] };
describe('BudgetCalendar', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [BudgetCalendar], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function render() { const f = TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month', month); f.detectChanges(); http.expectOne(r => r.url.endsWith('/income-period')).flush(period); f.detectChanges(); return f; }
  it('shows the current-period bill total and highlights its dates on a Monday-first calendar', () => {
    const f = render();
    expect(f.nativeElement.querySelector('.period-total').textContent).toContain('$100.00');
    expect(f.componentInstance.inPeriod('2026-10-16')).toBe(true);
    expect(f.componentInstance.inPeriod('2026-10-22')).toBe(true);
    expect(f.componentInstance.inPeriod('2026-10-23')).toBe(false);
    expect(f.componentInstance.days.slice(0, 3)).toEqual([null, null, null]); // October 1 is Thursday.
    expect(f.nativeElement.querySelectorAll('.day')).toHaveLength(31);
    expect(f.nativeElement.textContent).toContain('Bills on that day belong to the next period');
  });
  it('opens day details for touch or keyboard activation without changing the income period', () => {
    const f = render(); const day = f.nativeElement.querySelector('[aria-label^="2026-10-17"]') as HTMLButtonElement;
    day.click(); f.detectChanges();
    expect(f.componentInstance.selectedDay()).toBe('2026-10-17');
    expect(f.nativeElement.querySelector('.day-details').textContent).toContain('Electric');
    expect(day.getAttribute('aria-pressed')).toBe('true');
    expect(day.getAttribute('aria-label')).toContain('Bill: Electric, $100.00');
    http.expectNone(r => r.url.endsWith('/income-period'));
  });
  it('refreshes the unpaid total and calendar status after a payment save', () => {
    const f = render(); const c = f.componentInstance;
    c.paymentBill.set(bill);
    c.paymentSaved({ isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null });
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, unpaidBillsTotal: 0, bills: [{ ...bill, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null } }] });
    f.detectChanges();
    expect(c.month.events[0].payment?.isPaid).toBe(true);
    expect(c.paymentBill()).toBeNull();
    expect(f.nativeElement.querySelector('.period-total strong').textContent).toContain('$0.00');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-17"]').getAttribute('aria-label')).toContain('Paid');
  });
  it('keeps the selected income source and cancels stale period requests', () => {
    const f = render(); const c = f.componentInstance;
    const emit = vi.spyOn(c.sourceChange, 'emit');
    c.selectSource('salary-a'); const old = http.expectOne(r => r.params.get('sourceKey') === 'salary-a');
    c.selectSource(null); expect(old.cancelled).toBe(true);
    const req = http.expectOne(r => r.url.endsWith('/income-period')); expect(req.request.params.has('sourceKey')).toBe(false);
    req.flush(period); f.detectChanges(); expect(emit).toHaveBeenLastCalledWith(null);
    expect(c.period()!.endDate).toBe('2026-10-22');
  });
  it('shows errors, retry and incomplete-period states without inventing bills', () => {
    const f = render(); const c = f.componentInstance;
    c.loadPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({}, { status: 500, statusText: 'Unavailable' }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Your income period couldn’t load');
    expect(c.inPeriod('2026-10-17')).toBe(false);
    c.loadPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, startDate: null, endDate: null, bills: [], billsTotal: 0 }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('No published payday on or before');
    expect(f.nativeElement.querySelector('.period-total')).toBeNull();
    c.loadPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, endDate: null, nextPayday: null, bills: [] }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('no defined end');
  });
});
