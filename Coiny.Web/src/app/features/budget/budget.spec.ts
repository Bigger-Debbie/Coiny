import { SavingsFunds } from '../savings/savings-funds';
import { BudgetDashboard } from './budget-dashboard';
import { BudgetCalendar } from './budget-calendar';
import { BudgetCalendarStub, BudgetActualsStub, SavingsFundsStub } from './budget-calendar.stub';
import { BudgetActualsPanel } from './budget-actuals';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Budget } from './budget';
import { BudgetRevision, BudgetSetup } from '../../shared/models/budget/budget';

const draft: BudgetRevision = {
  id: 7, revisionNumber: 1, editVersion: 2, effectiveFromMonth: '2026-11-01', setupStep: 'Bills', state: 'Draft', publishedAt: null,
  incomeSources: [{ name: 'Salary', amount: 2000, schedule: { frequency: 'EveryTwoWeeks', startDate: '2026-10-02', endDate: null, dayOfMonth: null, secondDayOfMonth: null } }],
  allocations: [{ name: 'Groceries', amount: 600, allocationType: 'Spending', schedule: null }],
};
const setup: BudgetSetup = { currentMonth: '2026-10-01', hasPublishedBudget: false, currentRevisionId: null, firstPublishedMonth: null, draft };
const month = { month: '2026-11-01', revisionId: 7, expectedIncome: 4000, allocated: 600, unassigned: 3400, overBudget: 0, incomeSources: [], allocations: [], events: [] };

describe('Budget setup', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Budget], providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.overrideComponent(BudgetDashboard, { remove: { imports: [BudgetCalendar, BudgetActualsPanel, SavingsFunds] }, add: { imports: [BudgetCalendarStub, BudgetActualsStub, SavingsFundsStub] } });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); vi.restoreAllMocks(); });
  async function render(status = setup) {
    const f = TestBed.createComponent(Budget); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/budgets/setup')).flush(status);
    await f.whenStable(); f.detectChanges(); return f;
  }
  async function open(status = setup) {
    const f = await render(status);
    const opening = f.componentInstance.start();
    http.expectOne(r => r.url.endsWith('/preview')).flush({ draftId: 7, editVersion: 2, month });
    await opening; f.detectChanges(); return f;
  }
  it('shows first-time setup without creating a draft on page load', async () => {
    const f = await render({ ...setup, draft: null });
    expect(f.nativeElement.textContent).toContain('Set up your budget');
    expect(f.componentInstance.wizard()).toBe(false);
    http.expectNone(r => r.method === 'POST');
  });
  it('creates an empty household draft only when setup is started', async () => {
    const f = await render({ ...setup, draft: null });
    const opening = f.componentInstance.start();
    const start = http.expectOne(r => r.url.endsWith('/draft') && r.method === 'POST');
    expect(start.request.body).toEqual({ effectiveFromMonth: '2026-10-01' });
    start.flush({ ...draft, incomeSources: [], allocations: [], setupStep: 'Income', editVersion: 0 });
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/preview')).flush({ draftId: 7, editVersion: 0, month });
    await opening;
    expect(f.componentInstance.income.length).toBe(0);
  });
  it('resumes the saved step and uses the saved effective month and edit version for preview', async () => {
    const f = await open();
    expect(f.componentInstance.step()).toBe(1);
    expect(f.componentInstance.form.value.effectiveMonth).toBe('2026-11');
    expect(f.componentInstance.dirty).toBe(false);
    expect(f.nativeElement.textContent).toContain('$3,400.00');
  });
  it('saves all steps together, normalizes schedules, and refreshes the summary', async () => {
    const f = await open(); const c = f.componentInstance;
    c.income.at(0).patchValue({ healthInsurancePerPaycheck: 100, retirementContributionPerPaycheck: 200 });
    c.addAllocation();
    const bill = c.allocations.at(1);
    bill.patchValue({ name: ' Car insurance ', amount: 1200, allocationType: 'Bills', schedule: { frequency: 'EverySixMonths', startDate: '2026-11-15' } });
    expect(c.dirty).toBe(true);
    const saving = c.save(3);
    const req = http.expectOne(r => r.method === 'PUT');
    expect(req.request.body.expectedEditVersion).toBe(2);
    expect(req.request.body.setupStep).toBe('Spending');
    expect(req.request.body.incomeSources[0].schedule.dayOfMonth).toBeNull();
    expect(req.request.body.incomeSources[0].healthInsurancePerPaycheck).toBe(100);
    expect(req.request.body.incomeSources[0].retirementContributionPerPaycheck).toBe(200);
    expect(req.request.body.allocations[1].schedule.dayOfMonth).toBeNull();
    expect(req.request.body.allocations[1].schedule.secondDayOfMonth).toBeNull();
    expect(req.request.body.allocations.map((a: { name: string }) => a.name)).toEqual(['Groceries', 'Car insurance']);
    req.flush({ ...draft, ...req.request.body, editVersion: 3 });
    await Promise.resolve();
    f.detectChanges();
    expect(f.nativeElement.querySelector('.summary #effective-month')).not.toBeNull();
    expect(f.nativeElement.querySelector('.editor #effective-month')).toBeNull();
    expect(f.nativeElement.querySelector('.summary').textContent).toContain('Updating your saved monthly totals');
    const preview = http.expectOne(r => r.url.endsWith('/preview'));
    expect(preview.request.params.get('expectedEditVersion')).toBe('3');
    expect(preview.request.params.get('month')).toBe('2026-11-01');
    preview.flush({ draftId: 7, editVersion: 3, month });
    await saving;
    expect(c.step()).toBe(3); expect(c.dirty).toBe(false);
  });
  it('adds scheduled subscriptions in their own wizard step', async () => {
    const f = await open(); const c = f.componentInstance;
    c.step.set(2); c.addAllocation(); f.detectChanges();
    const item = c.allocations.at(c.allocations.length - 1);
    expect(item.value.allocationType).toBe('Subscriptions');
    expect(item.get('schedule')).not.toBeNull();
    expect(c.visibleAllocation(item)).toBe(true);
    expect(f.nativeElement.querySelector('.editor h2').textContent).toContain('Subscriptions');
    c.step.set(1); expect(c.visibleAllocation(item)).toBe(false);
  });
  it('moves items between sections without replacing their identity or schedule', async () => {
    const f = await open(); const c = f.componentInstance;
    c.step.set(3); const item = c.allocations.at(0);
    item.patchValue({ sourceKey: 'stable-key', allocationType: 'Spending' });
    item.get('allocationType')!.setValue('Subscriptions');
    expect(item.value.sourceKey).toBe('stable-key');
    expect(item.value.schedule).not.toBeNull();
    expect(c.visibleAllocation(item)).toBe(false);
    c.step.set(2); expect(c.visibleAllocation(item)).toBe(true);
  });
  it('rejects blank names, fractional cents, and invalid twice-monthly dates before saving', async () => {
    const f = await open(); const c = f.componentInstance;
    c.income.at(0).patchValue({ name: ' ', amount: .001, schedule: { frequency: 'TwiceMonthly', dayOfMonth: 20, secondDayOfMonth: 15 } });
    await c.save();
    http.expectNone(r => r.method === 'PUT');
    expect(c.error()).toContain('Complete or remove');
  });
  it('blocks stale writes until the user reloads the shared draft', async () => {
    const f = await open(); const c = f.componentInstance;
    c.form.patchValue({ effectiveMonth: '2026-12' });
    const saving = c.save();
    http.expectOne(r => r.method === 'PUT').flush({ detail: 'This draft changed.' }, { status: 409, statusText: 'Conflict' });
    await saving;
    expect(c.conflict()).toBe(true); expect(c.dirty).toBe(true);
    await c.save(); http.expectNone(r => r.method === 'PUT');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const reload = c.reloadDraft();
    http.expectOne(r => r.url.endsWith('/setup')).flush({ ...setup, draft: { ...draft, editVersion: 3 } });
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/preview')).flush({ draftId: 7, editVersion: 3, month });
    await reload;
    expect(c.conflict()).toBe(false); expect(c.dirty).toBe(false);
    expect(c.draft()!.editVersion).toBe(3);
  });
  it('warns on unsaved navigation and before refresh, and blocks navigation while saving', async () => {
    const f = await open(); const c = f.componentInstance;
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    c.form.patchValue({ effectiveMonth: '2026-12' });
    expect(c.canLeave()).toBe(false); expect(confirm).toHaveBeenCalledOnce();
    const event = new Event('beforeunload', { cancelable: true });
    c.beforeUnload(event as BeforeUnloadEvent); expect(event.defaultPrevented).toBe(true);
    c.busy.set(true); confirm.mockClear(); expect(c.canLeave()).toBe(false); expect(confirm).not.toHaveBeenCalled();
  });
  it('saves and exits without losing resume position', async () => {
    const f = await open(); const c = f.componentInstance;
    const saving = c.save(1, true);
    const req = http.expectOne(r => r.method === 'PUT');
    req.flush({ ...draft, editVersion: 3 });
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/setup')).flush({ ...setup, draft: { ...draft, editVersion: 3 } });
    await saving; expect(c.wizard()).toBe(false); expect(c.draft()!.setupStep).toBe('Bills');
  });
  it('requires saving edits before publication and publishes only the reviewed version', async () => {
    const f = await open({ ...setup, draft: { ...draft, setupStep: 'Review' } }); const c = f.componentInstance;
    c.form.patchValue({ effectiveMonth: '2026-12' });
    await c.publish(); http.expectNone(r => r.url.endsWith('/publish'));
    c.form.patchValue({ effectiveMonth: '2026-11' });
    const publishing = c.publish();
    const req = http.expectOne(r => r.url.endsWith('/publish'));
    expect(req.request.body).toEqual({ expectedEditVersion: 2 }); req.flush({ ...draft, state: 'Published' });
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/setup')).flush({ ...setup, draft: null, hasPublishedBudget: true, firstPublishedMonth: '2026-11-01' });
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/month')).flush(month);
    await publishing; expect(c.wizard()).toBe(false); expect(c.notice()).toContain('published');
  });
  it('keeps the newest selected month when responses arrive out of order and exposes retryable errors', async () => {
    const f = await render(); const c = f.componentInstance;
    const older = c.loadPublished('2026-11-01');
    const oldRequest = http.expectOne(r => r.url.endsWith('/month'));
    const newer = c.loadPublished('2026-12-01');
    http.expectOne(r => r.url.endsWith('/month')).flush({ ...month, month: '2026-12-01' });
    await newer;
    oldRequest.flush(month); await older;
    expect(c.projection()!.month).toBe('2026-12-01');
    expect(c.selectedMonth()).toBe('2026-12-01');
    const failed = c.loadPublished('2027-01-01');
    http.expectOne(r => r.url.endsWith('/month')).flush({}, { status: 500, statusText: 'Unavailable' });
    await failed;
    expect(c.monthError()).toBe(true); expect(c.projection()).toBeNull(); expect(c.monthLoading()).toBe(false);
  });
  it('shows a future-only published plan without treating the household as new', async () => {
    const f = TestBed.createComponent(Budget); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/setup')).flush({ ...setup, draft: null, hasPublishedBudget: true, firstPublishedMonth: '2026-11-01' });
    await Promise.resolve();
    const req = http.expectOne(r => r.url.endsWith('/month'));
    expect(req.request.params.get('month')).toBe('2026-11-01'); req.flush(month);
    await vi.waitFor(() => expect(f.componentInstance.loading()).toBe(false)); f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('Edit budget');
    http.expectOne(r => r.url.endsWith('/income-period')).flush({ asOf:'2026-11-01', startDate:null,endDate:null,nextPayday:'2026-11-13',sources:[],bills:[],contributions:[],billsTotal:0,contributionsTotal:0 }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Your income-period outlook');
    expect(f.nativeElement.textContent).toContain('Manage budget items');
  });
});
