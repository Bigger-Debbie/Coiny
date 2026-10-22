import { Component, Input, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BudgetMonth, AllocationType } from '../../shared/models/budget/budget';
import { CategoryBreakdownItem } from '../../shared/models/transactions/category-breakdown';
import { BudgetCalendar } from './budget-calendar';
import { BudgetActualsPanel } from './budget-actuals';
import { DonutChart } from '../../shared/components/donut-chart/donut-chart';

const groups: { type: AllocationType; label: string }[] = [
  { type: 'Housing', label: 'Housing' }, { type: 'Bills', label: 'Bills' }, { type: 'Subscriptions', label: 'Subscriptions' },
  { type: 'Spending', label: 'Everyday spending' }, { type: 'Savings', label: 'Savings' },
  { type: 'Investments', label: 'Investing' },
];
@Component({
  selector: 'app-budget-dashboard', imports: [CurrencyPipe, DatePipe, FormsModule, DonutChart, BudgetCalendar, BudgetActualsPanel],
  templateUrl: './budget-dashboard.html', styleUrl: './budget-dashboard.css',
})
export class BudgetDashboard {
  @Input() month: BudgetMonth | null = null;
  @Input({ required: true }) selectedMonth = '';
  @Input() loading = false;
  @Input() error = false;
  readonly periodSource = signal<string | null>(null);
  readonly actualsRefresh = signal(0);
  refreshActuals() { this.actualsRefresh.update(value => value + 1); }
  readonly selectMonth = output<string>();
  private chartData: CategoryBreakdownItem[] = [];
  private chartMonth: BudgetMonth | null = null;
  get allocationSections() {
    return [
      { key: 'bills', label: 'Housing & bills', types: ['Housing', 'Bills'] },
      { key: 'subscriptions', label: 'Subscriptions', types: ['Subscriptions'] },
      { key: 'spending', label: 'Everyday spending', types: ['Spending'] },
      { key: 'savings', label: 'Savings & investing', types: ['Savings', 'Investments'] },
    ].map(section => {
      const items = this.month?.allocations.filter(item => section.types.includes(item.allocationType ?? '')) ?? [];
      return { ...section, items, total: items.reduce((sum, item) => sum + item.plannedAmount, 0) };
    });
  }
  get total() { return this.month ? Math.max(this.month.expectedIncome, this.month.allocated) : 0; }
  get categories(): CategoryBreakdownItem[] {
    if (this.month === this.chartMonth) return this.chartData;
    this.chartMonth = this.month;
    const month = this.month;
    this.chartData = month ? groups.map<CategoryBreakdownItem>((group, index) => ({
      categoryId: index + 1, categoryName: group.label,
      amount: month.allocations.filter(a => a.allocationType === group.type).reduce((sum, a) => sum + a.plannedAmount, 0),
      transactionCount: 0, percentage: 0,
    })).concat([{ categoryId: 7, categoryName: 'Potential savings', color: '#047c04', strokeWidth: 16, amount: month.unassigned, transactionCount: 0, percentage: 0 }])
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
