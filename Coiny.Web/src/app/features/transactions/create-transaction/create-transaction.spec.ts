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
    http.expectOne(r => r.url.endsWith('/accounts')).flush([{ id: 2, name: 'Savings', isActive: true, openingBalanceDate: '2020-01-01' }]);
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
});
