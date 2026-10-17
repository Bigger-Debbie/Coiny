import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { Accounts } from './accounts';
import { getTrendDates } from '../dashboard/trend-range';
import { By } from '@angular/platform-browser';
import { CreateAccount } from './create-account/create-account';
import { CreateTransaction } from '../transactions/create-transaction/create-transaction';
import { CreateCategory } from './create-category/create-category';
import { Component, Input } from '@angular/core';
import { CategoryBreakdownPanel } from './category-breakdown/category-breakdown';

@Component({ selector: 'app-category-breakdown', template: '' })
class BreakdownStub {
  @Input() accountId: number | null = null;
  @Input() startDate = '';
  @Input() endDate = '';
  @Input() refreshKey = 0;
}

const accounts = [
  { id: 1, name: 'Same name', institutionId: 1, institutionName: 'Bank', accountType: 'Checking', currentBalance: 1000, openingBalance: 900, openingBalanceDate: '2026-01-01', isActive: true },
  { id: 2, name: 'Same name', institutionId: null, institutionName: null, accountType: 'CreditCard', currentBalance: 200, openingBalance: 300, openingBalanceDate: '2026-01-01', isActive: true },
];
const trend = { points: [{ date: '2026-10-01', value: 800 }], startingValue: 800, endingValue: 800, absoluteChange: 0, percentageChange: 0, direction: 'Neutral' };
describe('Accounts', () => {
  let http: HttpTestingController;
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function(this: HTMLDialogElement) { this.setAttribute('open', ''); } });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function(this: HTMLDialogElement) { this.removeAttribute('open'); } });
  });
  afterAll(() => {
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal');
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'close');
  });
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Accounts], providers: [
      provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
    ] });
    TestBed.overrideComponent(Accounts, { remove: { imports: [CategoryBreakdownPanel] }, add: { imports: [BreakdownStub] } });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => http.verify());

  function render() {
    const f = TestBed.createComponent(Accounts);
    f.detectChanges();
    http.expectOne(r => r.url.endsWith('/accounts')).flush(accounts);
    http.expectOne(r => r.url.endsWith('/categories')).flush([{ id: 1, name: 'Groceries', categoryType: 'Expense', isActive: true, isSystem: false }]);
    http.expectOne(r => r.url.endsWith('/institutions')).flush([{ id: 1, name: 'Bank', website: 'javascript:alert(1)', isActive: true }]);
    return f;
  }
  function flushTrend() { http.expectOne(r => r.url.endsWith('/trend') || r.url.endsWith('/net-worth-trend')).flush(trend); }
  function flushPage(totalCount = 0, page = 1) {
    http.expectOne(r => r.url.endsWith('/paged')).flush({ items: [], totalCount, page, pageSize: 10 });
  }
  it('defaults to all accounts net worth, 1M, and ten results for the same dates', () => {
    const f = render();
    const dates = getTrendDates('1M');
    const trendRequest = http.expectOne(r => r.url.endsWith('/net-worth-trend'));
    const page = http.expectOne(r => r.url.endsWith('/paged'));
    for (const [key, value] of Object.entries(dates)) {
      expect(trendRequest.request.params.get(key)).toBe(value);
      expect(page.request.params.get(key)).toBe(value);
    }
    expect(page.request.params.get('pageSize')).toBe('10');
    expect(page.request.params.has('accountId')).toBe(false);
    trendRequest.flush(trend);
    page.flush({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    f.detectChanges();
    expect(f.nativeElement.querySelector('.balance').textContent).toContain('$800.00');
    expect(f.nativeElement.textContent).toContain('No transactions match');
  });
  it('scopes duplicate names by ID, cancels older requests, and shows amount owed', () => {
    const f = render();
    const oldTrend = http.expectOne(r => r.url.endsWith('/net-worth-trend'));
    const oldPage = http.expectOne(r => r.url.endsWith('/paged'));
    f.componentInstance.selectAccount('2');
    expect(oldTrend.cancelled).toBe(true);
    expect(oldPage.cancelled).toBe(true);
    const selectedTrend = http.expectOne(r => r.url.endsWith('/2/trend'));
    expect(selectedTrend.request.params.has('startDate')).toBe(true);
    selectedTrend.flush({ ...trend, absoluteChange: -100, direction: 'Down', percentageChange: null });
    const page = http.expectOne(r => r.url.endsWith('/paged'));
    expect(page.request.params.get('accountId')).toBe('2');
    page.flush({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Amount owed');
    expect(f.nativeElement.querySelector('.balance').textContent).toContain('$200.00');
    expect(f.nativeElement.querySelector('.balance-change.positive')).toBeTruthy();
    expect(f.nativeElement.querySelector('.balance-change').textContent).not.toContain('(');
  });
  it('applies type/category/search together, and resets page on filters and range changes', () => {
    const f = render();
    flushTrend(); flushPage(45);
    f.componentInstance.loadTransactions(3);
    flushPage(45, 3);
    f.componentInstance.search = ' coffee ';
    f.componentInstance.categoryType = 'Expense';
    f.componentInstance.categoryId = '1';
    f.componentInstance.applyFilters();
    const request = http.expectOne(r => r.url.endsWith('/paged'));
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('search')).toBe('coffee');
    expect(request.request.params.get('categoryId')).toBe('1');
    expect(request.request.params.get('categoryType')).toBe('Expense');
    request.flush({ items: [], totalCount: 45, page: 1, pageSize: 10 });
    f.componentInstance.loadTransactions(2); flushPage(45, 2);
    f.componentInstance.selectRange('1W');
    flushTrend();
    const week = http.expectOne(r => r.url.endsWith('/paged'));
    expect(week.request.params.get('page')).toBe('1');
    expect(week.request.params.get('startDate')).toBe(getTrendDates('1W').startDate);
    week.flush({ items: [], totalCount: 0, page: 1, pageSize: 10 });
  });
  it('shows bottom pagination and retries transaction errors', () => {
    const f = render();
    flushTrend(); flushPage(52);
    f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.pagination button').length).toBe(7);
    f.nativeElement.querySelector('[aria-label="Next page"]').click();
    const request = http.expectOne(r => r.url.endsWith('/paged'));
    expect(request.request.params.get('page')).toBe('2');
    request.flush('failed', { status: 500, statusText: 'Error' });
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Your transactions couldn’t load');
    f.componentInstance.loadTransactions();
    flushPage(52, 2); f.detectChanges();
    expect(f.nativeElement.querySelector('[aria-current="page"]').textContent.trim()).toBe('2');
  });
  it('does not create unsafe website links and keeps Add Account outside the picker', () => {
    const f = render(); flushTrend(); flushPage();
    f.componentInstance.selectAccount('1'); flushTrend(); flushPage();
    expect(f.componentInstance.website()).toBeNull();
    f.detectChanges();
    expect(f.nativeElement.querySelector('.page-heading button').textContent).toContain('Add Account');
    expect(f.nativeElement.querySelector('.balance-card .add-account')).toBeNull();
    expect(f.componentInstance.accountOptions().some(o => o.value === 'add')).toBe(false);
  });
  it('opens Add Account as a modal and refreshes the new account without losing range or filters', async () => {
    const f = render(); flushTrend(); flushPage();
    f.componentInstance.selectRange('1W'); flushTrend(); flushPage();
    f.componentInstance.search = 'coffee'; f.componentInstance.applyFilters(); flushPage();
    f.detectChanges();
    const trigger = f.nativeElement.querySelector('.page-heading button') as HTMLButtonElement;
    trigger.focus(); trigger.click(); f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Add Account');
    http.expectOne(r => r.url.endsWith('/institutions')).flush([]);
    const form = f.debugElement.query(By.directive(CreateAccount)).componentInstance as CreateAccount;
    expect(form.embedded).toBe(true);
    form.saved.emit({ ...accounts[0], id: 9 });
    http.expectOne(r => r.url.endsWith('/accounts')).flush([...accounts, { ...accounts[0], id: 9 }]);
    http.expectOne(r => r.url.endsWith('/categories')).flush([]);
    http.expectOne(r => r.url.endsWith('/institutions')).flush([]);
    http.expectOne(r => r.url.endsWith('/9/trend')).flush(trend);
    const page = http.expectOne(r => r.url.endsWith('/paged'));
    expect(page.request.params.get('accountId')).toBe('9');
    expect(page.request.params.get('search')).toBe('coffee');
    expect(page.request.params.get('startDate')).toBe(getTrendDates('1W').startDate);
    page.flush({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    f.detectChanges();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    expect(f.componentInstance.selectedId()).toBe(9);
    expect(f.componentInstance.range()).toBe('1W');
    expect(document.activeElement).toBe(trigger);
  });
  it('preselects the account in the transaction modal and cancels without leaving the page', async () => {
    const f = render(); flushTrend(); flushPage();
    f.componentInstance.selectAccount('2'); flushTrend(); flushPage();
    f.detectChanges();
    f.nativeElement.querySelector('.pane-actions .button-primary').click();
    f.detectChanges(); await f.whenStable();
    http.expectOne(r => r.url.endsWith('/accounts')).flush(accounts);
    http.expectOne(r => r.url.endsWith('/categories')).flush([{ id: 1, name: 'Groceries', isActive: true, categoryType: 'Expense' }]);
    const form = f.debugElement.query(By.directive(CreateTransaction)).componentInstance as CreateTransaction;
    expect(form.request.accountId).toBe(2);
    form.cancel(); f.detectChanges();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    expect(f.componentInstance.selectedId()).toBe(2);
    expect(TestBed.inject(Router).navigate).not.toHaveBeenCalledWith(['/transactions/new']);
  });
  it('adds a household category to filters and makes it available in the transaction form', async () => {
    const f = render(); flushTrend(); flushPage();
    f.detectChanges();
    f.nativeElement.querySelector('.pane-actions button.button-outline').click();
    f.detectChanges(); await f.whenStable();
    const category = { id: 8, name: 'Entertainment', categoryType: 'Expense', isSystem: false, isActive: true };
    const form = f.debugElement.query(By.directive(CreateCategory)).componentInstance as CreateCategory;
    form.saved.emit(category);
    f.detectChanges();
    expect(f.nativeElement.querySelector('dialog')).toBeNull();
    expect(f.componentInstance.categoryOptions().some(o => o.value === '8' && o.label === 'Entertainment')).toBe(true);
    expect(f.componentInstance.selectedId()).toBeNull();
    f.nativeElement.querySelector('.pane-actions .button-primary').click();
    f.detectChanges(); await f.whenStable();
    http.expectOne(r => r.url.endsWith('/accounts')).flush(accounts);
    http.expectOne(r => r.url.endsWith('/categories')).flush([category]);
    const transaction = f.debugElement.query(By.directive(CreateTransaction)).componentInstance as CreateTransaction;
    expect(transaction.categoryOptions.some(o => o.value === 8)).toBe(true);
    transaction.cancel(); f.detectChanges();
  });
  it('scopes the breakdown to account/range and refreshes it after saves without tying it to list filters', () => {
    const f = render(); flushTrend(); flushPage(); f.detectChanges();
    const panel = f.debugElement.query(By.directive(BreakdownStub)).componentInstance as BreakdownStub;
    const initialRefresh = panel.refreshKey;
    f.componentInstance.selectAccount('2'); flushTrend(); flushPage();
    f.componentInstance.selectRange('1W'); flushTrend(); flushPage(); f.detectChanges();
    expect(panel.accountId).toBe(2);
    expect(panel.startDate).toBe(getTrendDates('1W').startDate);
    f.componentInstance.search = 'coffee'; f.componentInstance.applyFilters(); flushPage(); f.detectChanges();
    expect(panel.refreshKey).toBe(initialRefresh);
    f.componentInstance.transactionCreated();
    http.expectOne(r => r.url.endsWith('/accounts')).flush(accounts);
    http.expectOne(r => r.url.endsWith('/categories')).flush([]);
    http.expectOne(r => r.url.endsWith('/institutions')).flush([]);
    flushTrend(); flushPage(); f.detectChanges();
    expect(panel.refreshKey).toBeGreaterThan(initialRefresh);
    expect(panel.accountId).toBe(2);
  });
});
