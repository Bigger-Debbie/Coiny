import { BudgetItemEditor } from './budget-item-editor';
import { BudgetService } from '../../core/services/budget.service';
import { getTrendDates } from '../dashboard/trend-range';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { Component, DestroyRef, inject, Input, OnChanges, SimpleChanges, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BudgetMonth, BudgetIncomePeriod, AllocationType, BudgetItemSelection, BudgetMonthlyItem } from '../../shared/models/budget/budget';
import { CategoryBreakdownItem } from '../../shared/models/transactions/category-breakdown';
import { BudgetCalendar } from './budget-calendar';
import { BudgetActualsPanel } from './budget-actuals';
import { DonutChart } from '../../shared/components/donut-chart/donut-chart';

const groups: { type: AllocationType; label: string; color: string }[] = [
  { type: 'Housing', label: 'Housing', color: '#2f305f' }, { type: 'Bills', label: 'Bills', color: '#eb9a50' }, { type: 'Subscriptions', label: 'Subscriptions', color: '#486c9b' },
  { type: 'Spending', label: 'Everyday spending', color: '#167b83' }, { type: 'Savings', label: 'Savings', color: '#7b4a96' },
  { type: 'Investments', label: 'Investing', color: '#d4a027' },
];
@Component({
  selector: 'app-budget-dashboard', imports: [CurrencyPipe, DatePipe, FormsModule, DonutChart, BudgetCalendar, BudgetActualsPanel, BudgetItemEditor],
  templateUrl: './budget-dashboard.html', styleUrl: './budget-dashboard.css',
})
export class BudgetDashboard implements OnChanges {
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private forecastRequest?: Subscription;
  readonly expectedRemainder = signal<number | null>(null);
  readonly remainderLoading = signal(false);
  readonly remainderError = signal(false);
  loadRemainder() {
    this.forecastRequest?.unsubscribe(); this.expectedRemainder.set(null); this.remainderError.set(false);
    const period = this.period();
    if (this.mode() !== 'period' || !period?.plan || !period.startDate) { this.remainderLoading.set(false); return; }
    this.remainderLoading.set(true);
    this.forecastRequest = this.api.forecast(period.startDate, 0).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: data => {
        const end = data.points.at(-1);
        const value = end?.forecast ?? end?.actual ?? null;
        this.expectedRemainder.set(value); this.remainderError.set(value === null); this.remainderLoading.set(false);
      },
      error: () => { this.remainderError.set(true); this.remainderLoading.set(false); },
    });
  }
  readonly mode = signal<'period' | 'month'>('period');
  readonly period = signal<BudgetIncomePeriod | null>(null);
  readonly periodLoading = signal(false);
  readonly periodError = signal(false);
  readonly today = getTrendDates('1D').endDate;
  private referenceDate?: string;
  get displayPlan() { return this.mode() === 'period' ? this.period()?.plan ?? null : this.month; }
  ngOnChanges(changes: SimpleChanges) {
    if (this.mode() === 'period' && !this.loading && !this.error && this.month?.revisionId != null) {
      if (changes['selectedMonth'] || !this.period()) this.referenceDate = this.selectedMonth.slice(0,7) === this.today.slice(0,7) ? undefined : this.selectedMonth;
      this.loadPeriod();
    }
  }
  selectMode(mode: 'period' | 'month') {
    this.mode.set(mode);
    if (mode === 'month') { this.forecastRequest?.unsubscribe(); this.expectedRemainder.set(null); }
    if (mode === 'period') this.loadPeriod();
    else if (this.period()?.startDate && this.period()!.startDate!.slice(0,7) !== this.selectedMonth.slice(0,7))
      this.selectMonth.emit(this.period()!.startDate!.slice(0,7) + '-01');
  }
  loadPeriod(asOf = this.referenceDate) {
    this.forecastRequest?.unsubscribe(); this.expectedRemainder.set(null);
    this.referenceDate = asOf; this.request?.unsubscribe(); this.periodLoading.set(true); this.periodError.set(false);
    this.request = this.api.incomePeriod(null,asOf).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: period => { this.period.set(period); this.periodLoading.set(false); this.loadRemainder(); },
      error: () => { this.periodError.set(true); this.periodLoading.set(false); },
    });
  }
  navigatePeriod(direction: number) {
    if (this.periodLoading()) return;
    if (direction === 0) { this.referenceDate = undefined; this.loadPeriod(); return; }
    const date = direction > 0 ? this.period()?.nextPayday : this.period()?.startDate;
    if (!date || direction < 0 && date <= '0001-01-02') return;
    const d = new Date(date + 'T00:00:00Z'); if (direction < 0) d.setUTCDate(d.getUTCDate()-1);
    this.loadPeriod(d.toISOString().slice(0,10));
  }
  @Input() month: BudgetMonth | null = null;
  @Input({ required: true }) selectedMonth = '';
  @Input() loading = false;
  @Input() error = false;
  readonly periodSource = signal<string | null>(null);
  readonly actualsRefresh = signal(0);
  refreshActuals() { this.actualsRefresh.update(value => value + 1); if (this.mode() === 'period') this.loadPeriod(); }
  readonly selectMonth = output<string>();
  readonly budgetChanged = output<void>();
  readonly itemEditor = signal<{ selection: BudgetItemSelection | null } | null>(null);
  manageItem(item: BudgetMonthlyItem, isIncome: boolean) { this.itemEditor.set({ selection: { sourceKey:item.sourceKey,isIncome } }); }
  addItem(isIncome: boolean, type?: string) { this.itemEditor.set({ selection: { isIncome,type:type as AllocationType,add:true } }); }
  itemSaved() { this.itemEditor.set(null); this.actualsRefresh.update(v=>v+1); this.budgetChanged.emit(); }
  private chartData: CategoryBreakdownItem[] = [];
  private chartMonth: BudgetMonth | null = null;
  get allocationSections() {
    return [
      { key: 'bills', label: 'Housing & bills', types: ['Housing', 'Bills'] },
      { key: 'subscriptions', label: 'Subscriptions', types: ['Subscriptions'] },
      { key: 'spending', label: 'Everyday spending', types: ['Spending'] },
      { key: 'savings', label: 'Savings & investing', types: ['Savings', 'Investments'] },
    ].map(section => {
      const items = this.displayPlan?.allocations.filter(item => section.types.includes(item.allocationType ?? '')) ?? [];
      return { ...section, items, total: items.reduce((sum, item) => sum + item.plannedAmount, 0) };
    });
  }
  get total() { const plan = this.displayPlan; return plan ? Math.max(plan.expectedIncome,plan.allocated) : 0; }
  get categories(): CategoryBreakdownItem[] {
    if (this.displayPlan === this.chartMonth) return this.chartData;
    this.chartMonth = this.displayPlan;
    const month = this.displayPlan;
    this.chartData = month ? groups.map<CategoryBreakdownItem>((group, index) => ({
      categoryId: index + 1, categoryName: group.label, color: group.color,
      amount: month.allocations.filter(a => a.allocationType === group.type).reduce((sum, a) => sum + a.plannedAmount, 0),
      transactionCount: 0, percentage: 0,
    })).concat([
      { categoryId: 7, categoryName: this.mode() === 'period' ? 'Expected remaining' : 'Monthly remainder', color: '#047c04', strokeWidth: 16,
        amount: month.unassigned, transactionCount: 0, percentage: 0 },
    ])
      .filter(item => item.amount > 0).map(item => ({ ...item, percentage: this.total ? item.amount / this.total * 100 : 0 })) : [];
    return this.chartData;
  }
  change(value: string) {
    if (/^\d{4}-(0[1-9]|1[0-2])$/.test(value) && value >= '0001-02') this.selectMonth.emit(value + '-01');
  }
  adjacent(direction: number): string | null {
    const [year, month] = this.selectedMonth.split('-').map(Number);
    const index = year * 12 + month - 1 + direction;
    if (index < 13 || index > 9999 * 12 + 11 || !Number.isFinite(index)) return null;
    return String(Math.floor(index / 12)).padStart(4, '0') + '-' + String(index % 12 + 1).padStart(2, '0');
  }
  move(direction: number) { const next = this.adjacent(direction); if (next && !this.loading) this.change(next); }
}
