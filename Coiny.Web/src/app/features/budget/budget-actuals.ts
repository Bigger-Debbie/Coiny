import { Component, DestroyRef, inject, Input, OnChanges, SimpleChanges, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { BudgetService } from '../../core/services/budget.service';
import { ToastService } from '../../core/services/toast.service';
import { BudgetActuals, BudgetActualsItem, BudgetActualsTransaction, BudgetCategoryDefault } from '../../shared/models/budget/budget-actuals';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';
import { Modal } from '../../shared/components/modal/modal';
import { BudgetRunningForecast } from './budget-running-forecast';
import { getTrendDates } from '../dashboard/trend-range';
import { BudgetComparisonChart } from './budget-comparison-chart';

@Component({
  selector: 'app-budget-actuals', imports: [CurrencyPipe, DatePipe, FormsModule, SelectMenu, Modal, BudgetComparisonChart, BudgetRunningForecast],
  templateUrl: './budget-actuals.html', styleUrl: './budget-actuals.css',
})
export class BudgetActualsPanel implements OnChanges {
  @Input({ required: true }) month = '';
  @Input() periodAsOf: string | null = null;
  @Input() refreshKey = 0;
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  private readonly toasts = inject(ToastService);
  private request?: Subscription;
  private itemRequest?: Subscription;
  readonly chartView = signal<'allocations' | 'forecast'>('allocations');
  readonly forecastRefresh = signal(0);
  get forecastAsOf() { const today=getTrendDates('1D').endDate; return this.periodAsOf ?? (this.month.slice(0,7)===today.slice(0,7) ? today : this.month); }
  readonly refreshed = output<void>();
  refresh() { this.refreshed.emit(); this.forecastRefresh.update(v=>v+1); this.load(); }
  readonly selectedItem = signal<BudgetActualsItem | null>(null);
  readonly itemReport = signal<BudgetActuals | null>(null);
  readonly itemLoading = signal(false);
  readonly itemError = signal('');
  get itemPages() { return Math.max(1, Math.ceil((this.itemReport()?.totalCount ?? 0) / 10)); }
  closeItem() { this.itemRequest?.unsubscribe(); this.selectedItem.set(null); }
  showItem(item: BudgetActualsItem, page = 1) {
    this.itemRequest?.unsubscribe(); this.selectedItem.set(item); this.itemReport.set(null);
    this.itemLoading.set(true); this.itemError.set('');
    this.itemRequest = (this.periodAsOf ? this.api.periodActuals(this.periodAsOf, 'all', '', page, item.allocationKey) : this.api.actuals(this.month, 'all', '', page, item.allocationKey))
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: report => {
          this.itemReport.set(report);
          this.selectedItem.set(report.items.find(i => i.allocationKey === item.allocationKey) ?? item);
          this.itemLoading.set(false);
        },
        error: () => { this.itemLoading.set(false); this.itemError.set('These transactions couldn’t load. Try again.'); },
      });
  }
  readonly report = signal<BudgetActuals | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly saving = signal(false);
  readonly editing = signal<BudgetActualsTransaction | null>(null);
  readonly editError = signal('');
  readonly categoryChoices = new Map<number, string>();
  assignment = 'default'; view = 'unassigned'; type = 'Expense'; page = 1;
  readonly views: SelectOption[] = [{ value: 'unassigned', label: 'Not assigned to budget' }, { value: 'all', label: 'All recorded activity' }];
  readonly types: SelectOption[] = [{ value: 'Expense', label: 'Expenses' }, { value: 'Transfer', label: 'Transfers' }, { value: '', label: 'Expenses and transfers' }];
  get totalPages() { return Math.max(1, Math.ceil((this.report()?.totalCount ?? 0) / 10)); }
  get activeCategoryDefaults() { return this.report()?.categoryDefaults.filter(rule => rule.isActive) ?? []; }
  comparisonRemaining(item: BudgetActualsItem) { return Math.round((item.remaining + (item.priorPeriodActual ?? 0) + (item.futurePeriodActual ?? 0)) * 100) / 100; }
  get hasOtherPeriodPayments() { return this.report()?.items.some(i => i.priorPeriodActual || i.futurePeriodActual) ?? false; }
  contribution(item: BudgetActualsItem) { return ['Savings', 'Investments'].includes(item.allocationType); }
  get targets(): SelectOption[] { return [{ value: '', label: 'Not assigned' }, ...(this.report()?.items ?? []).map(i => ({ value: i.allocationKey, label: i.name, detail: i.allocationType === 'Investments' ? 'Investing' : i.allocationType }))]; }
  get assignmentTargets(): SelectOption[] {
    const allowed = (this.report()?.items ?? []).filter(i => this.editing()?.categoryType !== 'Transfer' || ['Savings', 'Investments'].includes(i.allocationType));
    return [{ value: 'default', label: this.editing()?.categoryType === 'Transfer' ? 'No contribution assignment' : 'Use category default' },
      { value: '', label: 'Leave unassigned' }, ...allowed.map(i => ({ value: i.allocationKey, label: i.name }))];
  }
  ngOnChanges(changes: SimpleChanges) { this.closeItem(); if (changes['month'] || changes['periodAsOf']) this.report.set(null); this.page = 1; this.categoryChoices.clear(); this.editing.set(null); this.load(); }
  load(page = this.page) {
    this.request?.unsubscribe(); this.loading.set(true); this.error.set(''); this.page = page;
    this.request = (this.periodAsOf ? this.api.periodActuals(this.periodAsOf,this.view,this.type,page) : this.api.actuals(this.month, this.view, this.type, page)).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: report => { this.report.set(report); this.page = report.page; this.categoryChoices.clear(); this.loading.set(false); },
      error: () => { this.error.set('The comparison couldn’t load. Try again.'); this.loading.set(false); },
    });
  }
  saveCategory(rule: BudgetCategoryDefault) {
    if (this.saving() || !this.categoryChoices.has(rule.categoryId)) return;
    this.saving.set(true);
    this.api.saveCategoryDefault(rule.categoryId, this.month, this.categoryChoices.get(rule.categoryId) || null, rule.editVersion)
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: () => { this.saving.set(false); this.toasts.success('Category default saved from this budget month forward.'); this.refresh(); },
        error: response => { this.saving.set(false); this.toasts.error(response.error?.detail || 'The default couldn’t save. Reload and try again.'); },
      });
  }
  edit(entry: BudgetActualsTransaction) {
    this.assignment = entry.assignmentSource === 'Override' ? entry.allocationKey ?? '' : 'default';
    this.editError.set(''); this.editing.set(entry);
  }
  saveAssignment() {
    const entry = this.editing(); if (!entry || this.saving()) return;
    this.saving.set(true); this.editError.set('');
    this.api.assignTransaction(entry.id, entry.date.slice(0,7) + '-01', this.assignment === 'default' ? null : this.assignment || null,
      this.assignment === 'default', entry.editVersion, entry.pairedEditVersion).pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: () => { this.saving.set(false); this.editing.set(null); this.toasts.success('Budget assignment updated.'); this.refresh(); },
        error: response => { this.saving.set(false); this.editError.set(response.error?.detail || 'The assignment couldn’t save. Reload and try again.'); },
      });
  }
}
