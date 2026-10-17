import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { CreateTransaction } from './create-transaction';

describe('CreateTransaction', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CreateTransaction], providers: [
      provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ accountId: '2' }) } } },
    ] });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => http.verify());
  function render() {
    const f = TestBed.createComponent(CreateTransaction); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/accounts')).flush([
      { id: 2, name: 'Savings', isActive: true, openingBalanceDate: '2020-01-01' },
      { id: 3, name: 'Spending', isActive: true, openingBalanceDate: '2026-01-01' },
    ]);
    http.expectOne(r => r.url.endsWith('/categories')).flush([
      { id: 1, name: 'Groceries', categoryType: 'Expense', isActive: true },
      { id: 2, name: 'Transfer', categoryType: 'Transfer', isActive: true },
    ]);
    f.detectChanges();
    return f;
  }
  it('preselects the current account and excludes unsupported transfers', async () => {
    const f = render(); await f.whenStable();
    expect(f.componentInstance.request.accountId).toBe(2);
    expect(f.componentInstance.categories.map(c => c.id)).toEqual([1]);
    f.componentInstance.cancel();
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/accounts'], { queryParams: { accountId: 2 } });
  });
  it('saves once and returns to the account after success', async () => {
    const f = render();
    await f.whenStable();
    Object.assign(f.componentInstance.request, { categoryId: 1, amount: 12, description: 'Lunch' });
    f.detectChanges(); await f.whenStable();
    const form = f.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    f.detectChanges(); await f.whenStable();
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transactions'));
    expect(request.request.body.accountId).toBe(2);
    request.flush({ id: 5 });
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/accounts'], { queryParams: { accountId: 2 } });
  });
  it('creates and selects a category without losing the transaction draft', async () => {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    Object.assign(c.request, { amount: 45, description: 'Lunch', merchant: 'Cafe' });
    c.addingCategory.set(true); c.categoryName = ' Dining '; c.categoryType = 'Expense';
    c.saveCategory(); c.saveCategory();
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/categories'));
    expect(request.request.body).toEqual({ name: 'Dining', categoryType: 'Expense' });
    expect(c.busy()).toBe(true);
    c.closeCategory(); expect(c.addingCategory()).toBe(true);
    c.cancel(); expect(TestBed.inject(Router).navigate).not.toHaveBeenCalled();
    request.flush({ id: 3, name: 'Dining', categoryType: 'Expense', isActive: true });
    expect(c.request).toMatchObject({ accountId: 2, categoryId: 3, amount: 45, description: 'Lunch', merchant: 'Cafe' });
    expect(c.categories.map(category => category.id)).toContain(3);
    expect(c.addingCategory()).toBe(false);
    expect(c.busy()).toBe(false);
  });
  it('validates names and keeps a failed income category draft available to retry', async () => {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    c.addingCategory.set(true); c.saveCategory();
    expect(c.categoryError()).toContain('name');
    http.expectNone(r => r.method === 'POST');
    c.categoryName = 'Bonus'; c.categoryType = 'Income'; c.saveCategory();
    const request = http.expectOne(r => r.method === 'POST');
    expect(request.request.body.categoryType).toBe('Income');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(c.categoryName).toBe('Bonus'); expect(c.categoryType).toBe('Income');
    expect(c.addingCategory()).toBe(true); expect(c.busy()).toBe(false);
    expect(c.categoryError()).toContain('try again');
    c.closeCategory(); expect(c.request.categoryId).toBe(0);
  });
  it('saves a paired transfer once without a category or description', async () => {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    c.changeKind('Transfer'); c.toAccountId = 3;
    Object.assign(c.request, { amount: 125, transactionDate: '2026-10-16', isCleared: true, notes: 'Weekly spending' });
    f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('#categoryId')).toBeNull();
    expect(f.nativeElement.querySelector('#to-account')).toBeTruthy();
    const submit = () => f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    submit(); submit(); await f.whenStable();
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transfers'));
    expect(request.request.body).toEqual({ fromAccountId: 2, toAccountId: 3, amount: 125, date: '2026-10-16', description: null, notes: 'Weekly spending', isCleared: true });
    http.expectNone(r => r.url.endsWith('/transactions') && r.method === 'POST');
    expect(c.busy()).toBe(true);
    request.flush({ outgoing: { id: 1 }, incoming: { id: 2 } });
    expect(c.busy()).toBe(false);
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/accounts'], { queryParams: { accountId: 2 } });
  });
  it('blocks same-account transfers and dates before either opening balance', async () => {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    c.changeKind('Transfer'); c.toAccountId = 2; c.request.amount = 100;
    f.detectChanges(); await f.whenStable();
    const submit = () => f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    submit(); await f.whenStable();
    c.toAccountId = 3; c.request.transactionDate = '2025-12-31';
    f.changeDetectorRef.markForCheck();
    f.detectChanges(); await f.whenStable(); submit(); await f.whenStable();
    expect(c.openingDate).toBe('2026-01-01');
    http.expectNone(r => r.method === 'POST');
    expect(c.error()).toContain('opening date');
  });
  it('records only one side for unknown/external transfers and preserves errors for retry', async () => {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    c.changeKind('Transfer'); c.transferMode = 'Unknown'; c.direction = 'In';
    Object.assign(c.request, { amount: 45, description: 'Funds transfer' });
    f.detectChanges(); await f.whenStable();
    const submit = () => f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    submit(); await f.whenStable();
    const first = http.expectOne(r => r.url.endsWith('/transfers/entries'));
    expect(first.request.body).toMatchObject({ accountId: 2, direction: 'In', counterparty: 'Unknown', amount: 45 });
    first.flush({ detail: 'Try a different date.' }, { status: 400, statusText: 'Bad Request' });
    expect(c.request.description).toBe('Funds transfer'); expect(c.error()).toBe('Try a different date.');
    c.transferMode = 'External'; f.detectChanges(); await f.whenStable(); submit(); await f.whenStable();
    const retry = http.expectOne(r => r.url.endsWith('/transfers/entries'));
    expect(retry.request.body.counterparty).toBe('External'); retry.flush({ id: 3 });
  });
});
