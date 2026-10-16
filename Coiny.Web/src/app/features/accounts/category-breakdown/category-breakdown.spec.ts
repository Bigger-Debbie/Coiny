import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CategoryBreakdownPanel } from './category-breakdown';

const expenses = { categoryType: 'Expense', totalAmount: 100, transactionCount: 20,
  categories: [{ categoryId: 1, categoryName: 'Groceries', amount: 100, percentage: 100, transactionCount: 20 }] };
describe('CategoryBreakdownPanel', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CategoryBreakdownPanel], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function render() {
    const f = TestBed.createComponent(CategoryBreakdownPanel);
    f.componentRef.setInput('startDate', '2026-10-01');
    f.componentRef.setInput('endDate', '2026-10-15');
    f.detectChanges(); return f;
  }
  const isBreakdown = (r: { url: string }) => r.url.endsWith('/category-breakdown');
  it('defaults to expenses for the full household/date range', () => {
    const f = render();
    const request = http.expectOne(isBreakdown);
    expect(request.request.params.get('categoryType')).toBe('Expense');
    expect(request.request.params.get('startDate')).toBe('2026-10-01');
    expect(request.request.params.get('endDate')).toBe('2026-10-15');
    expect(request.request.params.has('accountId')).toBe(false);
    expect(request.request.params.has('page')).toBe(false);
    request.flush(expenses); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Total Expenses');
    expect(f.nativeElement.textContent).toContain('$100.00');
    expect(f.nativeElement.textContent).toContain('20 transactions');
  });
  it('cancels stale requests when toggling income or changing account/range', () => {
    const f = render();
    const old = http.expectOne(isBreakdown);
    f.componentInstance.selectType('Income');
    expect(old.cancelled).toBe(true);
    const income = http.expectOne(isBreakdown);
    expect(income.request.params.get('categoryType')).toBe('Income');
    f.componentRef.setInput('accountId', 2); f.componentRef.setInput('endDate', '2026-10-14'); f.detectChanges();
    expect(income.cancelled).toBe(true);
    const next = http.expectOne(isBreakdown);
    expect(next.request.params.get('accountId')).toBe('2');
    expect(next.request.params.get('endDate')).toBe('2026-10-14');
    next.flush({ ...expenses, categoryType: 'Income' }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Total Income');
    f.componentRef.setInput('refreshKey', 1); f.detectChanges();
    http.expectOne(isBreakdown).flush({ ...expenses, categoryType: 'Income' });
  });
  it('shows empty, error, and retry states', () => {
    const f = render();
    http.expectOne(isBreakdown).flush({ categoryType: 'Expense', totalAmount: 0, transactionCount: 0, categories: [] });
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('No expenses in this period.');
    f.componentInstance.selectType('Income');
    http.expectOne(isBreakdown).flush('Failed', { status: 500, statusText: 'Server error' });
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('Your breakdown couldn’t load.');
    f.nativeElement.querySelector('.state button').click();
    http.expectOne(isBreakdown).flush({ categoryType: 'Income', totalAmount: 0, transactionCount: 0, categories: [] });
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('No income in this period.');
  });
});
