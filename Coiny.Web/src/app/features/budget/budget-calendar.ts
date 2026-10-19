import { Component, DestroyRef, inject, Input, OnChanges, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BillPayment } from './bill-payment';
import { Subscription } from 'rxjs';
import { BudgetService } from '../../core/services/budget.service';
import { BillPaymentState, BudgetEvent, BudgetIncomePeriod, BudgetMonth } from '../../shared/models/budget/budget';
import { getTrendDates } from '../dashboard/trend-range';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-budget-calendar', imports: [CurrencyPipe, DatePipe, FormsModule, SelectMenu, BillPayment],
  templateUrl: './budget-calendar.html', styleUrl: './budget-calendar.css',
})
export class BudgetCalendar implements OnInit, OnChanges {
  @Input({ required: true }) month!: BudgetMonth;
  @Input() source: string | null = null;
  readonly sourceChange = output<string | null>();
  readonly selectMonth = output<string>();
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  readonly paymentBill = signal<BudgetEvent | null>(null);
  isBill(event: BudgetEvent) { return !event.isIncome && ['Housing', 'Bills'].includes(event.allocationType ?? ''); }
  status(event: BudgetEvent) { return event.payment?.isPaid ? 'Paid' : event.date < this.today ? 'Overdue' : event.date === this.today ? 'Due today' : 'Upcoming'; }
  paymentSaved(payment: BillPaymentState) {
    const bill = this.paymentBill();
    if (!bill) return;
    this.month = { ...this.month, events: this.month.events.map(e => e.sourceKey === bill.sourceKey && e.date === bill.date && this.isBill(e) ? { ...e, payment } : e) };
    this.paymentBill.set(null); this.loadPeriod();
  }
  readonly period = signal<BudgetIncomePeriod | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly sourceKey = signal<string | null>(null);
  readonly tooltipHidden = signal(false);
  readonly selectedDay = signal('');
  readonly today = getTrendDates('1D').endDate;
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  get sourceOptions(): SelectOption[] {
    return [{ value: null, label: 'Household income' }, ...(this.period()?.sources ?? []).map(s => ({ value: s.sourceKey, label: s.name }))];
  }
  ngOnInit() { this.sourceKey.set(this.source); this.loadPeriod(); }
  ngOnChanges() { this.selectedDay.set(this.today.startsWith(this.month.month.slice(0, 7)) ? this.today : this.month.month); }
  loadPeriod() {
    this.request?.unsubscribe(); this.loading.set(true); this.error.set(false);
    this.request = this.api.incomePeriod(this.sourceKey()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: period => { this.period.set(period); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }
  selectSource(value: string | number | null) { this.sourceKey.set(value === null ? null : String(value)); this.sourceChange.emit(this.sourceKey()); this.loadPeriod(); }
  resetSource() { this.selectSource(null); }
  get days() {
    const [year, month] = this.month.month.split('-').map(Number);
    const start = new Date(0); start.setUTCFullYear(year, month - 1, 1); start.setUTCHours(0, 0, 0, 0);
    const offset = (start.getUTCDay() + 6) % 7;
    const end = new Date(start); end.setUTCMonth(month, 0);
    return Array.from({ length: Math.ceil((offset + end.getUTCDate()) / 7) * 7 }, (_, index) => {
      const day = index - offset + 1;
      if (day < 1 || day > end.getUTCDate()) return null;
      const date = this.month.month.slice(0, 8) + String(day).padStart(2, '0');
      return { day, date, events: this.month.events.filter(event => event.date === date) };
    });
  }
  get selectedEvents() { return this.month.events.filter(e => e.date === this.selectedDay()); }
  inPeriod(date: string) {
    const period = this.period();
    return !this.loading() && !this.error() && !!period?.startDate && !!period.endDate && date >= period.startDate && date <= period.endDate;
  }
  kind(event: BudgetEvent) { return event.isIncome ? 'Income' : ['Housing', 'Bills'].includes(event.allocationType ?? '') ? 'Bill' : ['Savings', 'Investments'].includes(event.allocationType ?? '') ? 'Contribution' : 'Spending'; }
  dayLabel(date: string, events: BudgetEvent[]) {
    return date + (date === this.today ? ', today' : '') + (this.inPeriod(date) ? ', current income period' : '') + (events.length ? ': ' + events.map(e => `${this.kind(e)}: ${e.name}, $${e.amount.toFixed(2)}${this.isBill(e) ? ", " + this.status(e) : ""}`).join('; ') : ', no scheduled items');
  }
}
