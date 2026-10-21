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
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush([]);
    f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('#categoryId')).toBeNull();
    expect(f.nativeElement.querySelector('#to-account')).toBeTruthy();
    const submit = () => f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    submit(); submit(); await f.whenStable();
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transfers'));
    expect(request.request.body).toEqual({ fromAccountId: 2, toAccountId: 3, amount: 125, date: '2026-10-16', description: null, notes: 'Weekly spending', isCleared: true, createBothEntries: false });
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
  const match = { id: 45, accountId: 3, accountName: 'Spending', editVersion: 2, transactionDate: '2026-10-15', transferDirection: 'In', amount: 125, description: 'Imported card payment' };
  async function transferDraft() {
    const f = render(); await f.whenStable(); const c = f.componentInstance;
    c.changeKind('Transfer'); c.toAccountId = 3;
    Object.assign(c.request, { amount: 125, transactionDate: '2026-10-16', description: 'Bills payment', isCleared: true });
    f.detectChanges(); await f.whenStable(); return f;
  }
  it('requires a choice when existing entries match and links the selected imported entry on save', async () => {
    const f = await transferDraft(); const c = f.componentInstance;
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush([match]);
    f.detectChanges(); expect(c.transferChoice).toBeNull();
    const save = f.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement; expect(save.disabled).toBe(true);
    f.nativeElement.querySelector('.match-choice').click(); f.detectChanges(); await f.whenStable();
    expect(c.transferChoice).toBe(45); expect(save.textContent).toContain('Create missing entry');
    expect(f.nativeElement.textContent).toContain('Only a new entry in Savings');
    expect(f.nativeElement.textContent).toContain('New entry is cleared');
    save.click(); await f.whenStable();
    const request = http.expectOne(r => r.url.endsWith('/transfers'));
    expect(request.request.body).toMatchObject({ existingTransactionId: 45, expectedExistingEditVersion: 2, fromAccountId: 2, toAccountId: 3, date: '2026-10-16', isCleared: true });
    expect(request.request.body.createBothEntries).toBeUndefined(); request.flush({ outgoing: { id: 46 }, incoming: match });
  });
  it('allows deliberately creating both sides after reviewing possible matches', async () => {
    const f = await transferDraft(); const c = f.componentInstance;
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush([match]);
    f.detectChanges(); f.nativeElement.querySelector('.match-choices .button-outline').click();
    f.detectChanges(); await f.whenStable(); f.nativeElement.querySelector('button[type="submit"]').click(); await f.whenStable();
    const request = http.expectOne(r => r.url.endsWith('/transfers')); expect(request.request.body.createBothEntries).toBe(true);
    expect(request.request.body.existingTransactionId).toBeUndefined(); request.flush({});
  });
  it('clears matches and decisions when transfer inputs change and cancels stale lookups', async () => {
    const f = await transferDraft(); const c = f.componentInstance;
    c.checkTransferMatches(); const first = http.expectOne(r => r.url.endsWith('/transfers/candidates'));
    c.request.amount = 150; c.transferInputsChanged(); expect(first.cancelled).toBe(true);
    c.checkTransferMatches(); const next = http.expectOne(r => r.url.endsWith('/transfers/candidates'));
    expect(next.request.body.amount).toBe(150); next.flush([]); expect(c.transferChecked).toBe(true);
    c.request.transactionDate = '2026-10-17'; expect(c.transferChecked).toBe(false);
    c.transferInputsChanged(); expect(c.transferChoice).toBeNull(); expect(c.transferMatches()).toEqual([]);
    c.changeKind('Expense');
  });
  it('keeps save blocked on a lookup failure and requires fresh review after a stale-match save conflict', async () => {
    const f = await transferDraft(); const c = f.componentInstance;
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush({}, { status: 500, statusText: 'Error' });
    f.detectChanges(); expect(c.matchError()).toBe(true); expect(f.nativeElement.querySelector('button[type="submit"]').disabled).toBe(true);
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush([match]); c.transferChoice = 45;
    f.detectChanges(); await f.whenStable(); f.nativeElement.querySelector('button[type="submit"]').click(); await f.whenStable();
    http.expectOne(r => r.url.endsWith('/transfers')).flush({ detail: 'This entry is already linked. Check again.' }, { status: 409, statusText: 'Conflict' });
    expect(c.transferChecked).toBe(false); expect(c.transferChoice).toBeNull(); expect(c.request.amount).toBe(125); expect(c.error()).toContain('already linked');
  });
  it('can select an existing outgoing entry to create only the incoming side', async () => {
    const f = await transferDraft(); const c = f.componentInstance;
    c.checkTransferMatches(); http.expectOne(r => r.url.endsWith('/transfers/candidates')).flush([{ ...match, accountId: 2, accountName: 'Savings', transferDirection: 'Out' }]);
    c.transferChoice = 45; expect(c.missingAccountName).toBe('Spending'); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Only a new entry in Spending');
  });
});
