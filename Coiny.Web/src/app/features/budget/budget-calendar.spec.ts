import { of } from 'rxjs';
import { BudgetService } from '../../core/services/budget.service';
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
  beforeEach(() => { TestBed.configureTestingModule({ imports: [BudgetCalendar], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController);
    const api=TestBed.inject(BudgetService); const original=api.month.bind(api);
    vi.spyOn(api,'month').mockImplementation(date=>date===month.month ? of(month) : original(date));
  });
  afterEach(() => http.verify());
  function render() { const f = TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month', month); f.detectChanges(); http.expectOne(r => r.url.endsWith('/income-period')).flush(period); f.detectChanges(); return f; }
  it('navigates using payday boundaries and preserves the income schedule', () => {
    const f = render(); const c = f.componentInstance;
    c.selectSource('salary-a'); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, sourceKey: 'salary-a' });
    c.nextPeriod();
    const next = http.expectOne(r => r.url.endsWith('/income-period'));
    expect(next.request.params.get('asOf')).toBe('2026-10-23'); expect(next.request.params.get('sourceKey')).toBe('salary-a');
    next.flush({ ...period, startDate: '2026-10-23', endDate: '2026-10-29', nextPayday: '2026-10-30' });
    expect(c.selectedDay()).toBe('2026-10-23');
    c.previousPeriod(); const previous = http.expectOne(r => r.url.endsWith('/income-period'));
    expect(previous.request.params.get('asOf')).toBe('2026-10-22'); previous.flush(period);
    c.currentPeriod(); const current = http.expectOne(r => r.url.endsWith('/income-period'));
    expect(current.request.params.has('asOf')).toBe(false); expect(current.request.params.get('sourceKey')).toBe('salary-a'); current.flush(period);
  });
  it('shows both full months for split periods with shared details and payment refreshes', () => {
    const f = render(); const c = f.componentInstance;
    c.nextPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, startDate: '2026-10-30', endDate: '2026-11-12', nextPayday: '2026-11-13' });
    const novemberBill = { ...bill, date: '2026-11-05', sourceKey: 'electric' };
    const november = { ...month, month: '2026-11-01', events: [novemberBill] };
    const req = http.expectOne(r => r.url.endsWith('/month')); expect(req.request.params.get('month')).toBe('2026-11-01'); req.flush(november); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.calendar-month')).toHaveLength(2);
    expect(f.nativeElement.querySelectorAll('.day')).toHaveLength(61);
    expect(c.inPeriod('2026-10-30')).toBe(true); expect(c.inPeriod('2026-11-12')).toBe(true); expect(c.inPeriod('2026-11-13')).toBe(false);
    (f.nativeElement.querySelector('[aria-label^="2026-11-05"]') as HTMLButtonElement).click(); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details').textContent).toContain('Electric');
    c.paymentBill.set(novemberBill); const payment = { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: 10 };
    c.paymentSaved(payment);
    const refresh = http.expectOne(r => r.url.endsWith('/income-period')); expect(refresh.request.params.get('asOf')).toBe('2026-10-23');
    refresh.flush({ ...period, startDate: '2026-10-30', endDate: '2026-11-12', nextPayday: '2026-11-13' });
    http.expectOne(r => r.url.endsWith('/month')).flush({ ...november, events: [{ ...novemberBill, payment }] }); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details').textContent).toContain('Paid · Linked');
    c.currentPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush(period); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.calendar-month')).toHaveLength(1);
  });
  it('handles December to January and cancels obsolete month requests', () => {
    const f = render(); const c = f.componentInstance;
    c.loadPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, startDate: '2026-12-25', endDate: '2027-01-07', nextPayday: '2027-01-08' });
    const old = http.match(r => r.url.endsWith('/month')); expect(old.map(r => r.request.params.get('month'))).toEqual(['2026-12-01', '2027-01-01']);
    c.currentPeriod(); expect(old.every(r => r.cancelled)).toBe(true);
    http.expectOne(r => r.url.endsWith('/income-period')).flush(period); expect(c.calendars()).toEqual([month]);
    c.loadPeriod(); http.expectOne(r => r.url.endsWith('/income-period')).flush({ ...period, startDate: '2026-12-25', endDate: '2027-01-07' });
    const requests = http.match(r => r.url.endsWith('/month')); requests[0].flush({ ...month, month: '2026-12-01', events: [] }); requests[1].flush({ ...month, month: '2027-01-01', events: [] }); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.day')).toHaveLength(62);
  });
  it('uses the parent period and shows its generated goal without separate navigation or period requests', () => {
    const goal={ ...bill, name:'Car fund', sourceKey:'car', date:'2026-10-22', isPeriodSetAside:true, allocationType:'Savings' as const };
    const selected={ ...period, contributions:[goal],contributionsTotal:225.81,plan:{ ...month,events:[...month.events,goal] } };
    const f=TestBed.createComponent(BudgetCalendar); f.componentRef.setInput('month',month); f.componentRef.setInput('controlledPeriod',selected); f.detectChanges();
    http.expectNone(r=>r.url.endsWith('/income-period')); expect(f.nativeElement.querySelector('.period-navigation')).toBeNull();
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-22"] .markers').textContent).toContain('●');
    expect(f.nativeElement.querySelector('.period-panel').textContent).toContain('Planned set-aside');
    expect(f.nativeElement.querySelector('.period-total strong').textContent).toContain('$325.81');
    const emit=vi.spyOn(f.componentInstance.paymentChanged,'emit'); f.componentInstance.paymentBill.set(goal);
    f.componentInstance.paymentSaved({ isPaid:true,paidOn:'2026-10-16',transactionId:null,note:null });
    http.expectNone(r=>r.url.endsWith('/income-period')); expect(emit).toHaveBeenCalled();
  });
  it('uses refreshed period payment states over stale monthly calendar data for pending, linked, and unpaid bills', () => {
    const f=TestBed.createComponent(BudgetCalendar);
    const occurrence={ ...bill, sourceKey:'baby-sitter' };
    const staleMonth={ ...month, events:[occurrence] };
    vi.mocked(TestBed.inject(BudgetService).month).mockReturnValue(of(staleMonth));
    const payment={ isPaid:true,paidOn:'2026-10-16',transactionId:null as number | null,note:null };
    const makePeriod=(state: typeof payment) => ({ ...period,bills:[{ ...occurrence,payment:state }],plan:{ ...staleMonth,events:[{ ...occurrence,payment:state }] } });
    f.componentRef.setInput('month',staleMonth); f.componentRef.setInput('controlledPeriod',makePeriod(payment)); f.detectChanges();
    f.componentInstance.selectedDay.set(bill.date); f.detectChanges();
    expect(f.nativeElement.querySelector('.period-panel .awaiting-payment')).toBeTruthy();
    expect(f.nativeElement.querySelector('.day-details .awaiting-payment').textContent).toContain('Link transaction');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-17"] .markers').textContent).toContain('◷');
    f.componentRef.setInput('controlledPeriod',makePeriod({ ...payment,transactionId:42 })); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details .paid-bill').textContent).toContain('View payment');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-17"] .markers').textContent).toContain('✓');
    f.componentRef.setInput('controlledPeriod',makePeriod({ ...payment,isPaid:false })); f.detectChanges();
    expect(f.nativeElement.querySelector('.day-details .awaiting-payment')).toBeNull();
    expect(f.nativeElement.querySelector('.day-details .paid-bill')).toBeNull();
    expect(f.nativeElement.querySelector('.day-details').textContent).toContain('Mark paid');
    expect(f.componentInstance.days.filter(Boolean).flatMap(day=>day!.events)).toHaveLength(1);
    http.expectNone(r=>r.url.endsWith('/income-period'));
  });
  it('refreshes payment status outside the selected period when navigating away from the due period', () => {
    const occurrence={ ...bill,date:'2026-10-15',sourceKey:'mazda' };
    const payment={ isPaid:true,paidOn:'2026-10-16',transactionId:410,note:null };
    const api=TestBed.inject(BudgetService);
    vi.mocked(api.month).mockReturnValue(of({ ...month,events:[{ ...occurrence,payment }] }));
    const f=TestBed.createComponent(BudgetCalendar);
    f.componentRef.setInput('month',{ ...month,events:[occurrence] });
    f.componentRef.setInput('controlledPeriod',{ ...period,startDate:'2026-10-02',endDate:'2026-10-15',bills:[{ ...occurrence,payment }] }); f.detectChanges();
    f.componentRef.setInput('controlledPeriod',{ ...period,bills:[] }); f.detectChanges();
    f.componentInstance.selectedDay.set(occurrence.date); f.detectChanges();
    expect(api.month).toHaveBeenCalledTimes(2);
    expect(f.nativeElement.querySelector('.day-details .paid-bill').textContent).toContain('View payment');
    expect(f.nativeElement.querySelector('[aria-label^="2026-10-15"] .markers').textContent).toContain('✓');
  });
  it('hides only completed linked payments when show completed is clicked off', async () => {
    const f = render(); const c = f.componentInstance;
    const linked = { ...bill, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: 42 } };
    const awaiting = { ...bill, payment: { isPaid: true, paidOn: '2026-10-16', note: null, transactionId: null } };
    c.period.set({ ...period, bills:[linked,awaiting,bill] }); f.detectChanges(); await f.whenStable();
    expect(c.visiblePayments([linked, awaiting, bill])).toHaveLength(3);
    const toggle = f.nativeElement.querySelector('.completed-toggle input') as HTMLInputElement;
    expect(toggle.checked).toBe(true); toggle.click(); f.detectChanges(); await f.whenStable();
    expect(toggle.checked).toBe(false); expect(c.showCompleted()).toBe(false);
    expect(c.visiblePayments([linked, awaiting, bill])).toEqual([awaiting, bill]);
    expect(f.nativeElement.querySelectorAll('.period-events .paid-bill')).toHaveLength(0);
    expect(f.nativeElement.querySelectorAll('.period-events .event-row')).toHaveLength(2);
    toggle.click(); f.detectChanges(); await f.whenStable();
    expect(toggle.checked).toBe(true); expect(f.nativeElement.querySelectorAll('.period-events .event-row')).toHaveLength(3);
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
