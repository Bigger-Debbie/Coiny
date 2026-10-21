import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetActualsPanel } from './budget-actuals';
import { BudgetActuals } from '../../shared/models/budget/budget-actuals';

const report: BudgetActuals = {
  month: '2026-10-01', throughDate: '2026-10-16', planned: 500, assignedActual: 100, unassignedExpenses: 40, awaitingTransaction: 200,
  items: [{ allocationKey: 'food', name: 'Groceries', allocationType: 'Spending', planned: 300, actual: 100, remaining: 200, awaitingTransaction: 0, transactionCount: 1 },
    { allocationKey: 'ira', name: 'IRA', allocationType: 'Investments', planned: 200, actual: 0, remaining: 200, awaitingTransaction: 200, transactionCount: 0 }],
  categoryDefaults: [{ categoryId: 1, categoryName: 'Food', allocationKey: null, isActive: true, editVersion: 0, effectiveFromMonth: null }],
  transactions: [{ id: 8, editVersion: 2, pairedEditVersion: null, date: '2026-10-05', description: 'Market', amount: 40, accountName: 'Bills', categoryName: 'Food', categoryType: 'Expense', allocationKey: null, allocationName: null, assignmentSource: 'Unassigned' }],
  page: 1, pageSize: 10, totalCount: 11,
};
describe('BudgetActualsPanel', () => {
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value() {} });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value() {} });
  });
  afterAll(() => { Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype, 'close'); });
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [BudgetActualsPanel], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function render() {
    const f = TestBed.createComponent(BudgetActualsPanel); f.componentRef.setInput('month', report.month); f.detectChanges();
    const request = http.expectOne(r => r.url.endsWith('/actuals')); expect(request.request.params.get('month')).toBe(report.month); expect(request.request.params.get('type')).toBe('Expense'); request.flush(report); f.detectChanges(); return f;
  }
  it('opens an item transaction breakdown independently of assignment filters and paginates it', () => {
    const f = render(); const c = f.componentInstance;
    (f.nativeElement.querySelector('.item-name') as HTMLButtonElement).click(); f.detectChanges();
    const request = http.expectOne(r => r.url.endsWith('/actuals'));
    expect(request.request.params.get('allocationKey')).toBe('food');
    expect(request.request.params.get('view')).toBe('all');
    expect(request.request.params.has('type')).toBe(false);
    request.flush({ ...report, transactions: [{ ...report.transactions[0], allocationKey: 'food', assignmentSource: 'Category', merchant: 'Demo Market' }] }); f.detectChanges();
    expect(f.nativeElement.querySelector('app-modal').textContent).toContain('Market');
    expect(f.nativeElement.querySelector('app-modal').textContent).toContain('Category default');
    expect(f.nativeElement.querySelector('app-modal .item-transaction span').firstChild.textContent).toContain('Demo Market');
    c.showItem(report.items[0], 2);
    const next = http.expectOne(r => r.url.endsWith('/actuals'));
    expect(next.request.params.get('page')).toBe('2');
    next.flush({ ...report, page: 2 });
    c.closeItem(); f.detectChanges(); expect(f.nativeElement.querySelector('app-modal')).toBeNull();
    expect(c.view).toBe('unassigned'); expect(c.type).toBe('Expense');
  });
  it('separates planned, actual, unmatched expenses, and awaiting estimates', () => {
    const f = render(); expect(f.nativeElement.textContent).toContain('Recorded against budget'); expect(f.nativeElement.textContent).toContain('$100.00');
    expect(f.nativeElement.querySelector('.awaiting').textContent).toContain('$200.00'); expect(f.nativeElement.textContent).toContain('These amounts are separate from actuals');
    expect(f.nativeElement.textContent).toContain('Market'); expect(f.nativeElement.textContent).toContain('Not assigned to budget');
    expect(f.nativeElement.querySelector('.budget-items').open).toBe(true);
    expect(f.nativeElement.querySelector('.defaults').open).toBe(false);
    expect(f.nativeElement.querySelector('.assignments').open).toBe(false);
  });
  it('saves a versioned category default for the selected month and refreshes without collapsing the editor', () => {
    const f = render(); const c = f.componentInstance; const details = f.nativeElement.querySelector('.defaults'); details.open = true;
    c.categoryChoices.set(1, 'food'); c.saveCategory(report.categoryDefaults[0]);
    const request = http.expectOne(r => r.method === 'PUT'); expect(request.request.body).toEqual({ month: report.month, allocationKey: 'food', expectedEditVersion: 0 }); request.flush(null);
    f.detectChanges(); expect(f.nativeElement.querySelector('.defaults')).toBe(details); expect(details.open).toBe(true);
    http.expectOne(r => r.url.endsWith('/actuals')).flush(report);
  });
  it('writes individual assignments with reviewed transaction versions and can restore defaults', () => {
    const f = render(); const c = f.componentInstance; c.edit(report.transactions[0]); c.assignment = 'ira'; c.saveAssignment();
    const request = http.expectOne(r => r.method === 'PUT'); expect(request.request.body).toEqual({ month: report.month, allocationKey: 'ira', useCategoryDefault: false, expectedEditVersion: 2, expectedPairedEditVersion: null }); request.flush(null);
    http.expectOne(r => r.url.endsWith('/actuals')).flush(report); expect(c.editing()).toBeNull();
    c.edit({ ...report.transactions[0], assignmentSource: 'Override' }); c.assignment = 'default'; c.saveAssignment();
    const reset = http.expectOne(r => r.method === 'PUT'); expect(reset.request.body.useCategoryDefault).toBe(true); reset.flush(null); http.expectOne(r => r.url.endsWith('/actuals')).flush(report);
  });
  it('offers only contribution targets for transfers and retains a stale assignment for correction', () => {
    const f = render(); const c = f.componentInstance; c.edit({ ...report.transactions[0], categoryType: 'Transfer', pairedEditVersion: 3 });
    expect(c.assignmentTargets.some(o => o.value === 'food')).toBe(false); expect(c.assignmentTargets.some(o => o.value === 'ira')).toBe(true);
    c.assignment = 'ira'; c.saveAssignment(); http.expectOne(r => r.method === 'PUT').flush({ detail: 'The transaction changed.' }, { status: 409, statusText: 'Conflict' });
    expect(c.editError()).toContain('transaction changed'); expect(c.editing()).not.toBeNull(); expect(c.saving()).toBe(false);
  });
  it('cancels old month requests, scopes filters and paging, and handles retry', () => {
    const f = render(); const c = f.componentInstance; c.view = 'all'; c.type = 'Transfer'; c.load(2);
    const old = http.expectOne(r => r.params.get('page') === '2'); expect(old.request.params.get('type')).toBe('Transfer');
    f.componentRef.setInput('month', '2026-11-01'); f.detectChanges(); expect(old.cancelled).toBe(true);
    http.expectOne(r => r.params.get('month') === '2026-11-01').flush({}, { status: 500, statusText: 'Error' }); f.detectChanges(); expect(f.nativeElement.textContent).toContain('Try again');
    c.load(); http.expectOne(r => r.url.endsWith('/actuals')).flush({ ...report, month: '2026-11-01', throughDate: null, transactions: [] }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('This future month shows your plan');
  });
});
