import { Component, DestroyRef, ElementRef, HostListener, inject, OnInit, signal, ViewChild } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { AbstractControl, FormGroup, ReactiveFormsModule, UntypedFormArray, UntypedFormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BudgetService } from '../../core/services/budget.service';
import { AllocationType, BudgetAllocation, BudgetDraftRequest, BudgetIncome, BudgetMonth, BudgetRevision, BudgetSchedule, BudgetSetup, BudgetStep } from '../../shared/models/budget/budget';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';
import { BudgetDashboard } from './budget-dashboard';
import { BudgetScheduleEditor } from './budget-schedule';

const money = (control: AbstractControl): ValidationErrors | null => {
  const n = control.value;
  return typeof n === 'number' && Number.isFinite(n) && n >= 0 && n <= 9999999999999999 && Math.abs(n * 100 - Math.round(n * 100)) < .00001 ? null : { money: true };
};
const optionalMoney = (c: AbstractControl) => c.value === null || c.value === '' ? null : money(c);
const name = (c: AbstractControl) => typeof c.value === 'string' && c.value.trim().length > 0 && c.value.trim().length <= 100 ? null : { name: true };
const validDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value ?? '') && value >= '0001-01-02' &&
  Number.isFinite(Date.parse(value)) && new Date(value).toISOString().slice(0, 10) === value;
const scheduleValidator = (c: AbstractControl): ValidationErrors | null => {
  const s = c.value;
  if (!validDate(s.startDate) || (s.endDate && (!validDate(s.endDate) || s.endDate < s.startDate))) return { schedule: true };
  if (['Weekly', 'EveryTwoWeeks', 'EverySixMonths', 'Yearly'].includes(s.frequency)) return null;
  if (!Number.isInteger(s.dayOfMonth) || s.dayOfMonth < 1 || s.dayOfMonth > 31) return { schedule: true };
  return s.frequency === 'Monthly' || (s.frequency === 'TwiceMonthly' && Number.isInteger(s.secondDayOfMonth) && s.secondDayOfMonth > s.dayOfMonth && s.secondDayOfMonth <= 31) ? null : { schedule: true };
};

@Component({
  selector: 'app-budget', imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, SelectMenu, BudgetScheduleEditor, BudgetDashboard],
  templateUrl: './budget.html', styleUrl: './budget.css',
})
export class Budget implements OnInit {
  private readonly api = inject(BudgetService);
  private readonly fb = inject(UntypedFormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  readonly setup = signal<BudgetSetup | null>(null);
  readonly draft = signal<BudgetRevision | null>(null);
  readonly selectedMonth = signal('');
  readonly monthLoading = signal(false);
  readonly monthError = signal(false);
  private monthRequest = 0;
  readonly projection = signal<BudgetMonth | null>(null);
  readonly wizard = signal(false);
  readonly previewLoading = signal(false);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly conflict = signal(false);
  readonly attempted = signal(false);
  readonly step = signal(0);
  readonly steps: { key: BudgetStep; label: string; description: string }[] = [
    { key: 'Income', label: 'Income', description: 'What money does your household bring home? Enter each paycheck after taxes and deductions. If the amount varies, start with a typical amount you can reasonably count on.' },
    { key: 'Bills', label: 'Housing & bills', description: 'Add payments you regularly need to cover, like rent, mortgage, electricity, insurance, and subscriptions. Use a recent bill to estimate amounts that change.' },
    { key: 'Spending', label: 'Everyday spending', description: 'How much would you like to set aside each month for groceries, transportation, dining, and other day-to-day spending? Start with recent spending—you can adjust these amounts later.' },
    { key: 'SavingsAndInvestments', label: 'Savings & investing', description: 'Plan contributions toward an emergency fund, future purchases, or investments. Only include money coming from the take-home income above, so payroll deductions aren’t counted twice.' },
    { key: 'Review', label: 'Review', description: 'Does this plan feel workable? Check what’s left unassigned or whether you’ve planned more than your expected income. You can go back and adjust before publishing.' },
  ];
  readonly billTypes: SelectOption[] = [{ value: 'Housing', label: 'Housing' }, { value: 'Bills', label: 'Bill' }];
  readonly savingsTypes: SelectOption[] = [{ value: 'Savings', label: 'Savings' }, { value: 'Investments', label: 'Investing' }];
  readonly form = this.fb.group({ effectiveMonth: ['', Validators.required], incomeSources: this.fb.array([]), allocations: this.fb.array([]) });
  @ViewChild('stepHeading') private stepHeading?: ElementRef<HTMLElement>;
  private focusStep() { requestAnimationFrame(() => this.stepHeading?.nativeElement.focus()); }
  private snapshot = '';
  get income() { return this.form.get('incomeSources') as UntypedFormArray; }
  get allocations() { return this.form.get('allocations') as UntypedFormArray; }
  get dirty() { return this.wizard() && JSON.stringify(this.form.getRawValue()) !== this.snapshot; }
  get monthValid() { return /^\d{4}-\d{2}$/.test(this.form.value.effectiveMonth) && validDate(this.form.value.effectiveMonth + '-01'); }
  get publishableMonth() { return this.monthValid && this.form.value.effectiveMonth + '-01' >= (this.setup()?.currentMonth ?? ''); }
  group(control: AbstractControl) { return control as FormGroup; }
  schedule(control: AbstractControl) { return control.get('schedule') as FormGroup; }
  visibleAllocation(control: AbstractControl) {
    const type: AllocationType = control.value.allocationType;
    return this.step() === 1 ? ['Housing', 'Bills'].includes(type) : this.step() === 2 ? type === 'Spending' : ['Savings', 'Investments'].includes(type);
  }
  ngOnInit() { void this.load(); }
  private request<T>(observable: import('rxjs').Observable<T>) { return firstValueFrom(observable.pipe(takeUntilDestroyed(this.destroyRef))); }
  async load() {
    this.loading.set(true); this.error.set('');
    try {
      const setup = await this.request(this.api.setup());
      this.setup.set(setup); this.draft.set(setup.draft);
      if (setup.hasPublishedBudget) await this.loadPublished(setup.currentRevisionId ? setup.currentMonth : setup.firstPublishedMonth!);
    } catch (e) { this.fail(e); }
    finally { this.loading.set(false); }
  }
  async loadPublished(month: string) {
    if (!validDate(month) || !month.endsWith('-01')) return;
    const version = ++this.monthRequest;
    this.selectedMonth.set(month); this.monthLoading.set(true); this.monthError.set(false); this.projection.set(null);
    try {
      const result = await this.request(this.api.month(month));
      if (version === this.monthRequest) this.projection.set(result);
    } catch { if (version === this.monthRequest) this.monthError.set(true); }
    finally { if (version === this.monthRequest) this.monthLoading.set(false); }
  }
  async start() {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.notice.set('');
    ++this.monthRequest; this.monthLoading.set(false);
    try {
      const draft = this.draft() ?? await this.request(this.api.start(this.selectedMonth() > this.setup()!.currentMonth ? this.selectedMonth() : this.setup()!.currentMonth));
      this.hydrate(draft); this.wizard.set(true);
      await this.refreshPreview(); this.focusStep();
    } catch (e) { this.fail(e); }
    finally { this.busy.set(false); }
  }
  private hydrate(draft: BudgetRevision) {
    this.draft.set(draft);
    this.income.clear(); this.allocations.clear();
    this.form.patchValue({ effectiveMonth: draft.effectiveFromMonth.slice(0, 7) });
    draft.incomeSources.forEach(i => this.income.push(this.incomeGroup(i)));
    draft.allocations.forEach(a => this.allocations.push(this.allocationGroup(a)));
    this.step.set(Math.max(0, this.steps.findIndex(s => s.key === draft.setupStep)));
    this.snapshot = JSON.stringify(this.form.getRawValue());
    this.attempted.set(false); this.conflict.set(false);
  }
  private scheduleGroup(s?: BudgetSchedule) {
    return this.fb.group({ frequency: s?.frequency ?? 'Monthly', startDate: s?.startDate ?? this.setup()!.currentMonth,
      endDate: s?.endDate ?? '', dayOfMonth: s?.dayOfMonth ?? 1, secondDayOfMonth: s?.secondDayOfMonth ?? 15 }, { validators: scheduleValidator });
  }
  private incomeGroup(i?: BudgetIncome) {
    return this.fb.group({ sourceKey: i?.sourceKey ?? null, name: [i?.name ?? '', name], amount: [i?.amount ?? null, money],
      healthInsurancePerPaycheck: [i?.healthInsurancePerPaycheck ?? null, optionalMoney],
      retirementContributionPerPaycheck: [i?.retirementContributionPerPaycheck ?? null, optionalMoney], schedule: this.scheduleGroup(i?.schedule) });
  }
  private allocationGroup(a: BudgetAllocation) {
    const group = this.fb.group({ name: [a.name, name], amount: [a.amount, money], allocationType: a.allocationType, schedule: a.schedule ? this.scheduleGroup(a.schedule) : this.fb.control(null) });
    return group;
  }
  addIncome() { this.income.push(this.incomeGroup()); }
  addAllocation() {
    const type = this.step() === 1 ? 'Bills' : this.step() === 2 ? 'Spending' : 'Savings';
    const group = this.allocationGroup({ name: '', amount: null as unknown as number, allocationType: type, schedule: null });
    if (type === 'Bills') group.setControl('schedule', this.scheduleGroup());
    this.allocations.push(group);
  }
  toggleSchedule(control: AbstractControl, event: Event) {
    this.group(control).setControl('schedule', (event.target as HTMLInputElement).checked ? this.scheduleGroup() : this.fb.control(null));
  }
  private normalize(s: BudgetSchedule): BudgetSchedule {
    const anchored = ['Weekly', 'EveryTwoWeeks', 'EverySixMonths', 'Yearly'].includes(s.frequency);
    return { ...s, endDate: s.endDate || null, dayOfMonth: anchored ? null : s.dayOfMonth,
      secondDayOfMonth: s.frequency === 'TwiceMonthly' ? s.secondDayOfMonth : null };
  }
  private payload(step: number): BudgetDraftRequest {
    return { expectedEditVersion: this.draft()!.editVersion, effectiveFromMonth: this.form.value.effectiveMonth + '-01', setupStep: this.steps[step].key,
      incomeSources: this.income.getRawValue().map((i: BudgetIncome) => ({ ...i, name: i.name.trim(), schedule: this.normalize(i.schedule) })),
      allocations: this.allocations.getRawValue().map((a: BudgetAllocation) => ({ ...a, name: a.name.trim(), schedule: a.schedule ? this.normalize(a.schedule) : null })) };
  }
  async save(nextStep = this.step(), exit = false) {
    if (this.busy() || this.conflict()) return;
    this.attempted.set(true); this.form.markAllAsTouched();
    if (this.form.invalid || !this.monthValid) { this.error.set('Complete or remove unfinished entries before saving. Check names, amounts, and schedule dates.'); return; }
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      const saved = await this.request(this.api.save(this.draft()!.id, this.payload(nextStep)));
      this.hydrate(saved);
      this.notice.set('Draft saved.');
      if (exit) {
        this.wizard.set(false); this.projection.set(null); await this.load();
      } else { await this.refreshPreview(); this.focusStep(); }
    } catch (e) { this.fail(e); }
    finally { this.busy.set(false); }
  }
  async refreshPreview() {
    const draft = this.draft();
    if (!draft) return;
    this.projection.set(null);
    this.previewLoading.set(true);
    try {
      const preview = await this.request(this.api.preview(draft.id, draft.editVersion, draft.effectiveFromMonth));
      this.projection.set(preview.month);
    } finally { this.previewLoading.set(false); }
  }
  async retryPreview() {
    this.busy.set(true); this.error.set('');
    try { await this.refreshPreview(); } catch (e) { this.fail(e); } finally { this.busy.set(false); }
  }
  async publish() {
    if (this.busy() || this.dirty || this.conflict() || !this.publishableMonth || !this.projection()) return;
    this.busy.set(true); this.error.set('');
    try {
      await this.request(this.api.publish(this.draft()!.id, this.draft()!.editVersion));
      this.wizard.set(false); this.snapshot = ''; this.draft.set(null); this.projection.set(null);
      await this.load(); this.notice.set('Your household budget is published.');
    } catch (e) { this.fail(e); }
    finally { this.busy.set(false); }
  }
  async reloadDraft() {
    if (this.busy() || (this.dirty && !window.confirm('Reload the household draft? Your unsaved changes will be discarded.'))) return;
    this.busy.set(true); this.error.set('');
    try {
      const setup = await this.request(this.api.setup()); this.setup.set(setup);
      if (setup.draft) { this.hydrate(setup.draft); await this.refreshPreview(); }
      else { this.wizard.set(false); this.conflict.set(false); this.draft.set(null); await this.load(); }
    } catch (e) { this.fail(e); }
    finally { this.busy.set(false); }
  }
  private fail(e: unknown) {
    const response = e as { status?: number; error?: { detail?: string } };
    if (response.status === 409 || (this.wizard() && response.status === 404)) this.conflict.set(true);
    this.error.set(response.error?.detail ?? 'Your budget couldn’t be loaded or saved. Please try again.');
  }
  canLeave() { return !this.busy() && (!this.dirty || window.confirm('Leave budget setup? Your unsaved changes will be lost.')); }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.dirty || this.busy()) { event.preventDefault(); event.returnValue = ''; }
  }
}
