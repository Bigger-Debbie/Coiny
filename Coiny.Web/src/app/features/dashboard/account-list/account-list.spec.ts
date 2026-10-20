import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { AccountList } from './account-list';
import { DashboardAccount } from '../../../shared/models/dashboard/dashboard-account';

describe('AccountList', () => {
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideRouter([])] }); });
  function account(overrides: Partial<DashboardAccount> = {}): DashboardAccount {
    return {
      id: 1, name: 'Primary Checking', institutionName: 'Demo Bank', accountType: 'Checking', currentBalance: 1200,
      monthlyTrend: { startDate: '2026-09-15', endDate: '2026-10-15', absoluteChange: 200, percentageChange: 20, direction: 'Up' },
      ...overrides,
    };
  }
  function render(accounts: DashboardAccount[]) {
    const fixture = TestBed.createComponent(AccountList);
    fixture.componentRef.setInput('accounts', accounts);
    fixture.detectChanges();
    return fixture;
  }
  it('links the whole account row to that specific account with a keyboard-accessible anchor', () => {
    const f = render([account({ id: 7 })]); const link = f.nativeElement.querySelector('.account-open') as HTMLAnchorElement;
    expect(link.tagName).toBe('A'); expect(link.getAttribute('href')).toBe('/accounts?accountId=7');
    expect(link.getAttribute('aria-label')).toContain('Open account: Primary Checking');
  });
  it('shows institution initials, account names, balance, and month percentage', () => {
    const f = render([account()]);
    expect(f.nativeElement.querySelector('.institution-avatar').textContent).toBe('D');
    expect(f.nativeElement.textContent).toContain('Primary Checking');
    expect(f.nativeElement.textContent).toContain('Demo Bank');
    expect(f.nativeElement.querySelector('.account-balance').textContent).toContain('$1,200.00');
    expect(f.nativeElement.querySelector('.account-trend.positive').textContent).toContain('20.0%');
    expect(f.nativeElement.querySelector('.account-trend').getAttribute('aria-label')).toContain('increased by 20.0%');
  });
  it('colors growing debt red and falling debt green without reversing arrows', () => {
    const up = account({ accountType: 'CreditCard' });
    const down = account({ id: 2, accountType: 'Loan', monthlyTrend: { ...up.monthlyTrend, absoluteChange: -200, percentageChange: -20, direction: 'Down' } });
    const f = render([up, down]);
    const rows = f.nativeElement.querySelectorAll('.account-row');
    expect(rows[0].querySelector('.account-trend.negative')).toBeTruthy();
    expect(rows[0].querySelector('.account-trend').getAttribute('aria-label')).toContain('increased');
    expect(rows[1].querySelector('.account-trend.positive')).toBeTruthy();
    expect(rows[1].querySelector('.account-trend').getAttribute('aria-label')).toContain('decreased');
    expect(rows[0].querySelector('.account-balance').textContent).toContain('owed');
  });
  it('shows a neutral dash with an explanation when the baseline is zero', () => {
    const sample = account({ institutionName: null });
    sample.monthlyTrend.percentageChange = null;
    const f = render([sample]);
    expect(f.nativeElement.querySelector('.institution-avatar').textContent).toBe('P');
    expect(f.nativeElement.textContent).toContain('No institution');
    expect(f.nativeElement.querySelector('.account-trend').textContent.trim()).toBe('—');
    expect(f.nativeElement.querySelector('.account-trend').getAttribute('title')).toContain('starting balance was zero');
    expect(f.nativeElement.querySelector('.trend-icon')).toBeNull();
    expect(f.nativeElement.querySelector('.account-trend.positive, .account-trend.negative')).toBeNull();
    expect(f.nativeElement.querySelector('.account-trend').textContent).not.toContain('%');
  });
  it('shows unchanged and unavailable history without directional icons', () => {
    const flat = account({ monthlyTrend: { startDate: '2026-09-15', endDate: '2026-10-15', absoluteChange: 0, percentageChange: 0, direction: 'Neutral' } });
    const missing = account({ id: 2, monthlyTrend: { ...flat.monthlyTrend, absoluteChange: null, percentageChange: null } });
    const f = render([flat, missing]);
    expect(f.nativeElement.querySelector('.account-trend').textContent).toContain('0.0%');
    expect(f.nativeElement.textContent).toContain('No history');
    expect(f.nativeElement.querySelector('.trend-icon')).toBeNull();
  });
});
