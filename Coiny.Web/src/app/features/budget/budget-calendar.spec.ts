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
  it('hides only completed linked payments when show completed is off', () => {
    const f = render(); const c = f.componentInstance;
    const linked = { ...bill, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: 42 } };
    const awaiting = { ...bill, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null } };
    expect(c.visiblePayments([linked, awaiting, bill])).toHaveLength(3);
    c.showCompleted.set(false);
    expect(c.visiblePayments([linked, awaiting, bill])).toEqual([awaiting, bill]);
  });
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
  it('tracks contributions in both lists with paid borders, checks, actions, and remaining totals', () => {
    const contribution = { ...bill, sourceKey: 'ira-key', itemId: 3, name: 'IRA', allocationType: 'Investments' as const };
    const f = TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month', { ...month, events: [...month.events, contribution] }); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, contributionsTotal: 100, unpaidContributionsTotal: 100, contributions: [contribution] });
    const c = f.componentInstance; c.selectedDay.set(contribution.date); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details').textContent).toContain('Mark paid');
    expect(c.canTrackPayment(contribution)).toBe(true); expect(c.canTrackPayment(month.events[1])).toBe(false);
    c.paymentBill.set(contribution); const payment = { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: 12 };
    c.paymentSaved(payment);
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, contributionsTotal: 100, unpaidContributionsTotal: 0, contributions: [{ ...contribution, payment }] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.period-panel .paid-bill').textContent).toContain('✓IRA');
    expect(f.nativeElement.querySelector('.day-details .paid-bill').textContent).toContain('View payment');
    expect(f.nativeElement.querySelector('.period-panel').textContent).toContain('$0.00 remaining unpaid');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-17"]').getAttribute('aria-label')).toContain('Contribution: IRA, $100.00, Paid');
    expect(c.month.events.find(e => e.name === 'IRA')?.payment?.transactionId).toBe(12);
  });
  it('shows paid unlinked bills as awaiting transactions in both lists and calendar without counting them as unpaid', () => {
    const awaiting = { ...bill, sourceKey: 'electric-key', payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null } };
    const f = TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month', { ...month, events: [awaiting] }); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, bills: [awaiting], unpaidBillsTotal: 0, awaitingTransactionCount: 1, awaitingTransactionTotal: 100 });
    f.componentInstance.selectedDay.set(bill.date); f.detectChanges();
    expect(f.nativeElement.querySelector('.period-panel .awaiting-payment').textContent).toContain('Link transaction');
    expect(f.nativeElement.querySelector('.day-details .awaiting-payment').textContent).toContain('Paid · Awaiting transaction');
    expect(f.nativeElement.querySelector('.period-panel .paid-bill')).toBeNull();
    expect(f.nativeElement.querySelector('.period-total strong').textContent).toContain('$0.00');
    expect(f.nativeElement.querySelector('.awaiting-summary').textContent).toContain('1 payment awaiting a transaction');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-17"] .markers').textContent).toContain('◷');
    expect(f.componentInstance.paymentAction(awaiting)).toBe('Link transaction');
  });
  it('moves a contribution from awaiting to linked and back when a link is removed', () => {
    const contribution = { ...bill, sourceKey: 'ira-key', itemId: 3, name: 'IRA', allocationType: 'Investments' as const, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null as number | null } };
    const f = TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month', { ...month, events: [contribution] }); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, bills: [], contributions: [contribution] });
    const c = f.componentInstance; c.selectedDay.set(contribution.date); f.detectChanges();
    expect(c.status(contribution)).toBe('Paid · Awaiting transaction');
    c.openPayment(contribution); expect(c.linkOnOpen()).toBe(true);
    c.paymentSaved({ ...contribution.payment, transactionId: 12 });
    const linked = { ...contribution, payment: { ...contribution.payment, transactionId: 12 } };
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, bills: [], contributions: [linked] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details .paid-bill').textContent).toContain('Paid · Linked');
    expect(f.nativeElement.querySelector('.awaiting-summary')).toBeNull();
    c.paymentBill.set(linked); c.paymentSaved(contribution.payment);
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, bills: [], contributions: [contribution] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details .awaiting-payment')).toBeTruthy();
    expect(f.nativeElement.querySelector('.day-details .paid-bill')).toBeNull();
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
