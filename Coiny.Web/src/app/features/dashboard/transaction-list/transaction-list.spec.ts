import { TestBed } from '@angular/core/testing';
import { TransactionList } from './transaction-list';
import { DashboardTransaction } from '../../../shared/models/dashboard/dashboard-transaction';

describe('TransactionList', () => {
  function transaction(overrides: Partial<DashboardTransaction> = {}): DashboardTransaction {
    return {
      id: 1, accountName: 'Primary Checking', institutionName: 'Demo Bank', categoryName: 'Groceries',
      categoryType: 'Expense', amount: 92.35, transactionDate: '2026-10-15T00:00:00Z',
      description: 'Weekly groceries', merchant: 'Demo Market', ...overrides,
    };
  }
  function render(transactions: DashboardTransaction[]) {
    const fixture = TestBed.createComponent(TransactionList);
    fixture.componentRef.setInput('transactions', transactions);
    fixture.detectChanges();
    return fixture;
  }
  it('shows account, institution, description, merchant and calendar date', () => {
    const f = render([transaction()]);
    expect(f.nativeElement.querySelector('.institution-avatar').textContent).toBe('D');
    for (const text of ['Primary Checking', 'Demo Bank', 'Weekly groceries', 'Demo Market']) {
      expect(f.nativeElement.textContent).toContain(text);
    }
    const date = f.nativeElement.querySelector('time');
    expect(date.getAttribute('datetime')).toBe('2026-10-15');
    expect(date.textContent).toContain('Oct 15, 2026');
  });
  it('shows signed green income and red expenses using directional icons', () => {
    const f = render([transaction({ categoryType: 'Income', amount: 3200 }), transaction({ id: 2 })]);
    const rows = f.nativeElement.querySelectorAll('.transaction-row');
    expect(rows[0].querySelector('.transaction-amount.income').textContent).toContain('+$3,200.00');
    expect(rows[1].querySelector('.transaction-amount.expense').textContent).toContain('−$92.35');
    expect(rows[0].querySelector('.amount-icon')).toBeTruthy();
    expect(rows[1].querySelector('.amount-icon')).toBeTruthy();
  });
  it('uses account and category fallbacks without empty merchant labels', () => {
    const f = render([transaction({ institutionName: null, merchant: ' ', description: ' ' })]);
    expect(f.nativeElement.querySelector('.institution-avatar').textContent).toBe('P');
    expect(f.nativeElement.textContent).toContain('No institution');
    expect(f.nativeElement.querySelector('.transaction-details').textContent).toContain('Groceries');
    expect(f.nativeElement.querySelector('.transaction-details strong')).toBeNull();
  });
  it('preserves API order and dates without timezone shifts or transfer signs', () => {
    const f = render([transaction({ id: 9, description: 'Latest', transactionDate: '2026-10-15T00:00:00+14:00', categoryType: 'Transfer' }), transaction({ description: 'Earlier' })]);
    const rows = f.nativeElement.querySelectorAll('.transaction-row');
    expect(rows[0].textContent).toContain('Latest');
    expect(rows[1].textContent).toContain('Earlier');
    expect(rows[0].querySelector('time').getAttribute('datetime')).toBe('2026-10-15');
    expect(rows[0].querySelector('.amount-icon')).toBeNull();
    expect(rows[0].querySelector('.amount-details').textContent).toContain('Transfer');
    expect(rows[0].querySelector('.transaction-amount').textContent.trim()).toBe('$92.35');
  });
});
