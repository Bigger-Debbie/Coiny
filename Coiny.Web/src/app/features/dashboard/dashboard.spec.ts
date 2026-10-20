import { By } from '@angular/platform-browser';
import { EditTransaction } from '../transactions/edit-transaction/edit-transaction';
import { UserPreferencesService } from '../../core/services/user-preferences.service';
import { getTrendDates } from './trend-range';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Dashboard } from './dashboard';
import { TrendResult } from '../../shared/models/trends/trend-result';
import { AppHeader } from '../../shared/components/app-header/app-header';
const trend: TrendResult = {
  points: [{ date: '2026-10-01', value: 100 }, { date: '2026-10-02', value: 125 }],
  startingValue: 100, endingValue: 125, absoluteChange: 25, percentageChange: 25, direction: 'Up',
};
const summaryData = { netWorth: { currentValue: 500, retirement: 300, liquidFunds: 250, debt: 50 }, accounts: [], recentTransactions: [] };
describe('Dashboard', () => {
  let http: HttpTestingController;
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value() {} });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value() {} });
  });
  afterAll(() => { Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype, 'close'); });
  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [Dashboard, AppHeader], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    await TestBed.compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function render(personalized = true) {
    const f = TestBed.createComponent(Dashboard);
    f.detectChanges();
    const profile = http.expectOne(r => r.url.endsWith('/auth/me'));
    const household = http.expectOne(r => r.url.endsWith('/households/me'));
    if (personalized) {
      profile.flush({ id: 'user-1', email: 'alex@example.test', firstName: 'Alex' });
      household.flush({ id: 1, name: 'Ross', role: 'Owner' });
    } else {
      profile.flush('Unavailable', { status: 500, statusText: 'Server error' });
      household.flush('Not found', { status: 404, statusText: 'Not found' });
    }
    return { f, summary: http.expectOne(r => r.url.endsWith('/dashboard')), initial: http.expectOne(r => r.url.endsWith('/net-worth-trend')) };
  }
  it('loads 1M and keeps current net worth independent of trend ending value', () => {
    const { f, summary, initial } = render();
    expect(f.nativeElement.textContent).toContain('Loading your financial picture');
    expect(f.nativeElement.textContent).toContain('Loading your accounts');
    expect(f.nativeElement.textContent).toContain('Loading your transactions');
    expect(initial.request.params.has('startDate')).toBe(true);
    summary.flush(summaryData);
    initial.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('h1').textContent).toContain('$500.00');
    expect(f.nativeElement.querySelector('.change-value').textContent).toContain('$25.00');
    expect(f.nativeElement.querySelector('app-trend-chart')).toBeTruthy();
    expect(f.nativeElement.querySelector('[aria-pressed="true"]').textContent.trim()).toBe('1M');
    expect(f.nativeElement.textContent).toContain('No accounts yet');
    expect(f.nativeElement.textContent).toContain('No transactions yet');
    expect(f.nativeElement.querySelector('.welcome-panel h2').textContent).toBe('Welcome back, Alex');
    expect(f.nativeElement.querySelector('.welcome-panel p').textContent).toBe('The Ross household at a glance.');
    f.componentInstance.householdName.set('The Ross household');
    f.detectChanges();
    expect(f.nativeElement.querySelector('.welcome-panel p').textContent).toBe('The Ross household at a glance.');
    expect(Array.from(f.nativeElement.querySelectorAll('.range-controls button'), (button: any) => button.textContent.trim()))
      .toEqual(['1D', '1W', '1M', '3M', '6M', '1Y', 'YTD']);
  });
  it('shows Investments between Welcome and Retirement, including zero balances', () => {
    const { f, summary, initial } = render();
    summary.flush({ ...summaryData, netWorth: { ...summaryData.netWorth, hasInvestments: true, investments: 2500, hasMortgage: true, mortgage: 1000 } });
    initial.flush(trend); f.detectChanges();
    const sidebar = f.nativeElement.querySelector('.net-worth-sidebar');
    expect(sidebar.children[0].classList.contains('welcome-panel')).toBe(true);
    expect(sidebar.children[1].classList.contains('investments')).toBe(true);
    expect(sidebar.children[2].classList.contains('retirement')).toBe(true);
    expect(sidebar.style.getPropertyValue('--summary-count')).toBe('5');
    expect(sidebar.querySelector('.investments').textContent).toContain('$2,500.00');
    f.componentInstance.dashboard.set({ ...summaryData, netWorth: { ...summaryData.netWorth, hasInvestments: true, investments: 0 } }); f.detectChanges();
    expect(sidebar.querySelector('.investments').textContent).toContain('$0.00');
    f.componentInstance.dashboard.set(summaryData); f.detectChanges();
    expect(sidebar.querySelector('.investments')).toBeNull();
  });
  it('shows mortgage separately and clearly labels the excluded view', () => {
    const { f, summary, initial } = render();
    summary.flush({ ...summaryData, netWorth: { ...summaryData.netWorth, hasMortgage: true, mortgage: 250000, includeMortgageInNetWorth: false } }); initial.flush(trend); f.detectChanges();
    expect(f.nativeElement.querySelector('.net-worth-label').textContent).toBe('Net Worthexcluding mortgage');
    expect(f.nativeElement.querySelector('.summary-panel.mortgage').textContent).toContain('$250,000.00');
    expect(f.nativeElement.querySelector('.summary-panel.debt').textContent).toContain('Other Debt');
    f.componentInstance.dashboard.set(summaryData); f.detectChanges(); expect(f.nativeElement.querySelector('.summary-panel.mortgage')).toBeNull();
  });
  it('refreshes balances and the current chart range when personal settings are saved', () => {
    const { f, summary, initial } = render(); summary.flush(summaryData); initial.flush(trend);
    f.componentInstance.selectRange('1W'); http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush(trend);
    TestBed.inject(UserPreferencesService).save({ includeMortgageInNetWorth: false }).subscribe();
    http.expectOne(r => r.method === 'PUT').flush({ includeMortgageInNetWorth: false });
    http.expectOne(r => r.url.endsWith('/dashboard')).flush({ ...summaryData, netWorth: { ...summaryData.netWorth, includeMortgageInNetWorth: false } });
    const refreshed = http.expectOne(r => r.url.endsWith('/net-worth-trend')); expect(refreshed.request.params.get('startDate')).toBe(getTrendDates('1W').startDate); refreshed.flush(trend); f.detectChanges();
    expect(f.componentInstance.selectedRange()).toBe('1W'); expect(f.nativeElement.querySelector('.net-worth-label').textContent).toBe('Net Worthexcluding mortgage');
  });
  it('shares one profile request with the header', () => {
    const header = TestBed.createComponent(AppHeader);
    header.detectChanges();
    const { f, summary, initial } = render();
    summary.flush(summaryData);
    initial.flush(trend);
    header.detectChanges(); f.detectChanges();
    expect(header.nativeElement.querySelector('.avatar-button').textContent.trim()).toBe('A');
    expect(f.nativeElement.querySelector('.welcome-panel h2').textContent).toBe('Welcome back, Alex');
    http.expectNone(r => r.url.endsWith('/auth/me'));
  });
  it('cancels superseded range requests', () => {
    const { f, summary, initial } = render();
    summary.flush(summaryData);
    f.componentInstance.selectRange('3M');
    expect(initial.cancelled).toBe(true);
    http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush(trend);
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('the past 3 months');
  });
  it('keeps account trends fixed when the hero chart range changes', () => {
    const { f, summary, initial } = render();
    summary.flush({ ...summaryData, accounts: [{
      id: 1, name: 'Primary Checking', accountType: 'Checking', currentBalance: 1200, institutionName: 'Demo Bank',
      monthlyTrend: { startDate: '2026-09-15', endDate: '2026-10-15', absoluteChange: 200, percentageChange: 20, direction: 'Up' },
    }] });
    initial.flush(trend);
    f.componentInstance.selectRange('1D');
    http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('app-account-list').textContent).toContain('20.0%');
    expect(f.nativeElement.querySelector('app-account-list').textContent).toContain('1M change');
    http.expectNone(r => r.url.endsWith('/dashboard'));
  });
  it('renders recent activity from the dashboard response without add actions', () => {
    const { f, summary, initial } = render();
    summary.flush({ ...summaryData, recentTransactions: [{
      id: 1, accountName: 'Primary Checking', institutionName: 'Demo Bank', categoryName: 'Groceries',
      categoryType: 'Expense', amount: 92.35, transactionDate: '2026-10-15T12:00:00',
      description: 'Weekly groceries', merchant: 'Demo Market',
    }] });
    initial.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('app-transaction-list').textContent).toContain('Weekly groceries');
    expect(f.nativeElement.querySelector('app-transaction-list').textContent).toContain('−$92.35');
    expect([...f.nativeElement.querySelectorAll('.transactions-section button')].some((button: unknown) => /Add/.test((button as HTMLButtonElement).textContent ?? ''))).toBe(false);
    expect(f.nativeElement.querySelector('.transaction-open').getAttribute('aria-label')).toContain('View transaction: Weekly groceries');
    expect(f.nativeElement.querySelector('.accounts-section button')).toBeNull();
    http.expectNone(r => r.url.endsWith('/transactions'));
  });
  it('opens dashboard transaction details and refreshes totals and the selected trend range after changes', async () => {
    const { f, summary, initial } = render();
    summary.flush({ ...summaryData, recentTransactions: [{ id: 7, accountName: 'Checking', institutionName: 'Bank', categoryName: 'Food', categoryType: 'Expense', amount: 25, transactionDate: '2026-10-16', description: 'Dinner', merchant: 'Cafe' }] });
    initial.flush(trend); f.componentInstance.selectRange('1W'); http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush(trend); f.detectChanges();
    f.nativeElement.querySelector('.transaction-open').click(); f.detectChanges();
    const blocks = await f.getDeferBlocks(); await blocks[0].render(DeferBlockState.Complete); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/transactions/7')).flush({ transaction: { id: 7, editVersion: 0, accountId: 1, categoryId: 1, accountName: 'Checking', institutionName: 'Bank', categoryType: 'Expense', categoryName: 'Food', amount: 25, transactionDate: '2026-10-16', description: 'Dinner', merchant: 'Cafe', notes: 'Original note', isCleared: true }, pairedEditVersion: null, bill: null });
    http.expectOne(r => r.url.endsWith('/accounts')).flush([{ id: 1, name: 'Checking', isActive: true, openingBalanceDate: '2026-10-01' }]);
    http.expectOne(r => r.url.endsWith('/categories')).flush([{ id: 1, name: 'Food', categoryType: 'Expense', isActive: true }]);
    f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Transaction Details');
    expect(f.nativeElement.querySelector('dialog form')).toBeNull();
    const editor = f.debugElement.query(By.directive(EditTransaction)).componentInstance as EditTransaction;
    editor.changed.emit('updated'); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/dashboard')).flush(summaryData);
    const refreshed = http.expectOne(r => r.url.endsWith('/net-worth-trend'));
    expect(refreshed.request.params.get('startDate')).toBe(getTrendDates('1W').startDate); refreshed.flush(trend); f.detectChanges();
    expect(f.componentInstance.selectedRange()).toBe('1W'); expect(f.nativeElement.querySelector('dialog')).toBeNull();
  });
  it('retries a failed transactions pane using the existing summary request', () => {
    const { f, summary, initial } = render();
    summary.flush('Unavailable', { status: 500, statusText: 'Server error' });
    initial.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('.transactions-section').textContent).toContain('Your transactions couldn’t load');
    f.nativeElement.querySelector('.transactions-section button').click();
    http.expectOne(r => r.url.endsWith('/dashboard')).flush(summaryData);
    f.detectChanges();
    expect(f.nativeElement.querySelector('.transactions-section').textContent).toContain('No transactions yet');
    expect(f.nativeElement.querySelector('.accounts-section').textContent).toContain('No accounts yet');
  });

  it('offers 1D and renders directional trend icons', () => {
    const { f, summary, initial } = render();
    summary.flush(summaryData);
    initial.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('.change-icon')).toBeTruthy();
    expect(f.nativeElement.querySelector('.change-value.positive')).toBeTruthy();
    const dayButton = f.nativeElement.querySelector('[aria-label="the past day"]');
    dayButton.click();
    const request = http.expectOne(r => r.url.endsWith('/net-worth-trend'));
    const start = Date.parse(request.request.params.get('startDate')!);
    const end = Date.parse(request.request.params.get('endDate')!);
    expect(end - start).toBe(86400000);
    request.flush({ ...trend, absoluteChange: -25, percentageChange: -25, direction: 'Down' });
    f.detectChanges();
    expect(f.nativeElement.querySelector('.change-value.negative .change-icon')).toBeTruthy();
    expect(f.nativeElement.querySelector('[aria-pressed="true"]').textContent.trim()).toBe('1D');
  });
  it('hides unavailable percentages and shows empty history', () => {
    const { f, summary, initial } = render();
    summary.flush(summaryData);
    initial.flush({ ...trend, percentageChange: null });
    f.detectChanges();
    expect(f.nativeElement.querySelector('.change-percent')).toBeNull();
    expect(f.nativeElement.querySelector('.change-value').textContent).not.toContain('(');
    f.componentInstance.trend.set({ ...trend, absoluteChange: 0, percentageChange: 0, direction: 'Neutral' });
    f.detectChanges();
    expect(f.nativeElement.querySelector('.change-percent').textContent).toContain('0.0%');
    f.componentInstance.selectRange('1Y');
    http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush({ points: [], startingValue: null, endingValue: null, absoluteChange: null, percentageChange: null, direction: 'Neutral' });
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('No account history');
  });
  it('keeps generic greetings when profile or household requests fail', () => {
    const { f, summary, initial } = render(false);
    summary.flush(summaryData);
    initial.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('.welcome-panel h2').textContent).toBe('Welcome back');
    expect(f.nativeElement.querySelector('.welcome-panel p').textContent).toBe('Your household at a glance.');
    expect(f.nativeElement.querySelector('.change-percent').textContent).toContain('25.0%');
  });
  it('requests seven calendar days back through today when 1W is selected', () => {
    const { f, summary, initial } = render();
    summary.flush(summaryData);
    initial.flush(trend);
    f.componentInstance.selectRange('1W');
    const request = http.expectOne(r => r.url.endsWith('/net-worth-trend'));
    expect(Date.parse(request.request.params.get('endDate')!) - Date.parse(request.request.params.get('startDate')!)).toBe(7 * 86400000);
    request.flush(trend);
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('the past week');
  });
  it('recovers from trend errors independently of summary errors', () => {
    const { f, summary, initial } = render();
    summary.flush('Unavailable', { status: 500, statusText: 'Server error' });
    initial.flush('Unavailable', { status: 500, statusText: 'Server error' });
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Your trend couldn’t load');
    expect(f.nativeElement.textContent).toContain('Retry balances');
    expect(f.nativeElement.textContent).toContain('Your accounts couldn’t load');
    f.componentInstance.loadTrend();
    http.expectOne(r => r.url.endsWith('/net-worth-trend')).flush(trend);
    f.detectChanges();
    expect(f.nativeElement.querySelector('app-trend-chart')).toBeTruthy();
    expect(f.componentInstance.summaryError()).toBe(true);
    const retry = Array.from(f.nativeElement.querySelectorAll('button'))
      .find((button: any) => button.textContent.trim() === 'Retry accounts') as HTMLButtonElement;
    retry.click();
    http.expectOne(r => r.url.endsWith('/dashboard')).flush(summaryData);
    f.detectChanges();
    expect(f.componentInstance.summaryError()).toBe(false);
    expect(f.nativeElement.textContent).toContain('No accounts yet');
  });
});
