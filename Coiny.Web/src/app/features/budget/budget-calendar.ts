import { Component, DestroyRef, inject, Input, OnChanges, OnInit, SimpleChanges, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BillPayment } from './bill-payment';
import { forkJoin, Subscription } from 'rxjs';
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
  @Input() controlledPeriod: BudgetIncomePeriod | null = null;
  @Input() source: string | null = null;
  readonly sourceChange = output<string | null>();
  readonly selectMonth = output<string>();
  readonly paymentChanged = output<void>();
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private calendarRequest?: Subscription;
  private referenceDate?: string;
  readonly calendars = signal<BudgetMonth[]>([]);
  readonly calendarLoading = signal(false);
  readonly calendarError = signal(false);
  get periodTitle() {
    const p = this.period();
    return p?.startDate && p.endDate && this.today >= p.startDate && this.today <= p.endDate ? 'Current income period' : 'Income period';
  }
  previousPeriod() {
    const start = this.period()?.startDate;
    if (this.loading() || !start || start <= '0001-01-02') return;
    const date = new Date(start + 'T00:00:00Z'); date.setUTCDate(date.getUTCDate() - 1);
    this.referenceDate = date.toISOString().slice(0, 10); this.selectedDay.set(this.referenceDate); this.loadPeriod();
  }
  nextPeriod() {
    if (this.loading() || !this.period()?.nextPayday) return;
    this.referenceDate = this.period()!.nextPayday!; this.selectedDay.set(this.referenceDate); this.loadPeriod();
  }
  currentPeriod() { this.referenceDate = undefined; this.selectedDay.set(this.today); this.loadPeriod(); }
  loadCalendars() {
    this.calendarRequest?.unsubscribe(); this.calendarError.set(false);
    const p = this.period();
    const first = (p?.startDate ?? this.month.month).slice(0, 7) + '-01';
    const last = (p?.endDate ?? first).slice(0, 7) + '-01';
    const months: string[] = []; let date = first;
    // Annual income schedules may cover more than two months.
    while (date <= last && months.length < 13) {
      months.push(date);
      const d = new Date(date + 'T00:00:00Z'); d.setUTCMonth(d.getUTCMonth() + 1);
      date = d.toISOString().slice(0, 10); if (date.length !== 10) break;
    }
    this.calendarLoading.set(true);
    this.calendarRequest = forkJoin(months.map(m => this.api.month(m)))
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: calendars => {
          // Fetch full months on each refresh so payments outside the selected period
          // also stay current. Overlay generated set-asides and period occurrences.
          const period = this.period();
          const currentEvents = [...(period?.plan?.events.filter(e => e.isIncome) ?? []), ...(period?.bills ?? []), ...(period?.contributions ?? [])];
          const identity = (event: BudgetEvent) => `${event.date}:${event.isIncome}:${event.sourceKey ?? event.itemId}`;
          calendars = calendars.map(c => {
            const events = new Map(c.events.map(event => [identity(event), event]));
            for (const event of currentEvents.filter(e => e.date.startsWith(c.month.slice(0,7)))) events.set(identity(event),event);
            return { ...c, events: [...events.values()] };
          });
          this.calendars.set(calendars); this.calendarLoading.set(false);
          if (!calendars.some(c => this.selectedDay().startsWith(c.month.slice(0, 7))))
            this.selectedDay.set(p?.startDate ?? first);
        },
        error: () => { this.calendarLoading.set(false); this.calendarError.set(true); },
      });
  }
  readonly paymentBill = signal<BudgetEvent | null>(null);
  canTrackPayment(event: BudgetEvent) { return !event.isIncome && ['Housing', 'Bills', 'Subscriptions', 'Savings', 'Investments'].includes(event.allocationType ?? ''); }
  awaitingTransaction(event: BudgetEvent) { return this.canTrackPayment(event) && event.payment?.isPaid === true && event.payment.transactionId == null; }
  linkedPayment(event: BudgetEvent) { return this.canTrackPayment(event) && event.payment?.isPaid === true && event.payment.transactionId != null; }
  paymentMarker(event: BudgetEvent) { return this.linkedPayment(event) ? '✓' : this.awaitingTransaction(event) ? '◷' : '●'; }
  paymentAction(event: BudgetEvent) { return this.awaitingTransaction(event) ? 'Link transaction' : this.linkedPayment(event) ? 'View payment' : 'Mark paid'; }
  status(event: BudgetEvent) { return this.awaitingTransaction(event) ? 'Paid · Awaiting transaction' : this.linkedPayment(event) ? 'Paid · Linked' : event.date < this.today ? 'Overdue' : event.date === this.today ? 'Due today' : 'Upcoming'; }
  get awaitingPayments() { return [...(this.period()?.bills ?? []), ...(this.period()?.contributions ?? [])].filter(e => this.awaitingTransaction(e)); }
  get awaitingTotal() { return this.awaitingPayments.reduce((sum, item) => sum + item.amount, 0); }
  readonly showCompleted = signal(true);
  visiblePayments(events: BudgetEvent[]) { return this.showCompleted() ? events : events.filter(event => !this.linkedPayment(event)); }
  readonly linkOnOpen = signal(false);
  openPayment(event: BudgetEvent) { this.linkOnOpen.set(this.awaitingTransaction(event)); this.paymentBill.set(event); }
  paymentSaved(payment: BillPaymentState) {
    const bill = this.paymentBill();
    if (!bill) return;
    this.month = { ...this.month, events: this.month.events.map(e => e.sourceKey === bill.sourceKey && e.date === bill.date && this.canTrackPayment(e) ? { ...e, payment } : e) };
    this.paymentBill.set(null); if (!this.controlledPeriod) this.loadPeriod();
    this.paymentChanged.emit();
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
  ngOnInit() { this.sourceKey.set(this.source); if (!this.controlledPeriod) this.loadPeriod(); }
  ngOnChanges(changes: SimpleChanges) {
    if (this.controlledPeriod) {
      this.period.set(this.controlledPeriod); this.loading.set(false); this.error.set(false);
      if (changes['controlledPeriod']?.previousValue?.startDate !== this.controlledPeriod.startDate)
        this.selectedDay.set(this.controlledPeriod.startDate ?? this.controlledPeriod.asOf);
      this.loadCalendars(); return;
    }
    if (changes['controlledPeriod']?.previousValue) { this.sourceKey.set(this.source); this.loadPeriod(); }
    if (!changes['month']) return; this.calendarRequest?.unsubscribe(); this.calendars.set([this.month]); this.calendarLoading.set(false); this.calendarError.set(false); this.selectedDay.set(this.today.startsWith(this.month.month.slice(0, 7)) ? this.today : this.month.month); }
  loadPeriod() {
    this.request?.unsubscribe(); this.calendarRequest?.unsubscribe(); this.loading.set(true); this.error.set(false);
    this.request = this.api.incomePeriod(this.sourceKey(), this.referenceDate).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: period => { this.period.set(period); this.loading.set(false); this.loadCalendars(); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }
  selectSource(value: string | number | null) { this.sourceKey.set(value === null ? null : String(value)); this.sourceChange.emit(this.sourceKey()); this.loadPeriod(); }
  resetSource() { this.selectSource(null); }
  get days() { return this.daysFor(this.calendars()[0] ?? this.month); }
  daysFor(calendar: BudgetMonth) {
    const [year, month] = calendar.month.split('-').map(Number);
    const start = new Date(0); start.setUTCFullYear(year, month - 1, 1); start.setUTCHours(0, 0, 0, 0);
    const offset = (start.getUTCDay() + 6) % 7;
    const end = new Date(start); end.setUTCMonth(month, 0);
    return Array.from({ length: Math.ceil((offset + end.getUTCDate()) / 7) * 7 }, (_, index) => {
      const day = index - offset + 1;
      if (day < 1 || day > end.getUTCDate()) return null;
      const date = calendar.month.slice(0, 8) + String(day).padStart(2, '0');
      return { day, date, events: calendar.events.filter(event => event.date === date) };
    });
  }
  get selectedEvents() { return this.calendars().flatMap(c => c.events).filter(e => e.date === this.selectedDay()); }
  inPeriod(date: string) {
    const period = this.period();
    return !this.loading() && !this.error() && !!period?.startDate && !!period.endDate && date >= period.startDate && date <= period.endDate;
  }
  kind(event: BudgetEvent) { return event.isIncome ? 'Income' : ['Housing', 'Bills', 'Subscriptions'].includes(event.allocationType ?? '') ? 'Bill' : ['Savings', 'Investments'].includes(event.allocationType ?? '') ? 'Contribution' : 'Spending'; }
  dayLabel(date: string, events: BudgetEvent[]) {
    return date + (date === this.today ? ', today' : '') + (this.inPeriod(date) ? ', selected income period' : '') + (events.length ? ': ' + events.map(e => `${this.kind(e)}: ${e.name}, $${e.amount.toFixed(2)}${this.canTrackPayment(e) ? ", " + this.status(e) : ""}`).join('; ') : ', no scheduled items');
  }
}
