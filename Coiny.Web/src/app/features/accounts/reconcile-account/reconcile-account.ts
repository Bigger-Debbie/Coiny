import { Component, computed, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountReconciliationService } from '../../../core/services/account-reconciliation.service';
import { notificationSignal } from '../../../core/services/toast.service';
import { Account } from '../../../shared/models/account/account';
import { AccountReconciliation, ReconciliationCheck } from '../../../shared/models/account/account-reconciliation';
import { getTrendDates } from '../../dashboard/trend-range';

@Component({
  selector: 'app-reconcile-account',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './reconcile-account.html',
  styleUrl: './reconcile-account.css',
})
export class ReconcileAccount implements OnInit {
  readonly account = input.required<Account>();
  readonly initialStatement = input<{ date: string; balance: number } | null>(null);
  readonly saved = output<void>();
  readonly inspect = output<{ date: string; balance: number }>();
  private readonly service = inject(AccountReconciliationService);
  private readonly destroyRef = inject(DestroyRef);
  readonly busy = signal(false);
  readonly error = notificationSignal('error');
  readonly check = signal<ReconciliationCheck | null>(null);
  readonly history = signal<AccountReconciliation[]>([]);
  readonly historyLoading = signal(true);
  readonly historyError = signal(false);
  readonly isDebt = computed(() => ['CreditCard', 'Loan'].includes(this.account().accountType));
  readonly today = getTrendDates('1D').endDate;
  statementDate = this.today;
  statementBalance: number | null = null;
  reviewed = false;

  ngOnInit() {
    const initial = this.initialStatement();
    if (initial) { this.statementDate = initial.date; this.statementBalance = initial.balance; }
    this.loadHistory();
  }
  private message(response: { error?: unknown }, fallback: string): string {
    if (typeof response.error === 'string') return response.error;
    const detail = (response.error as { detail?: unknown } | null)?.detail;
    return typeof detail === 'string' ? detail : fallback;
  }
  changed() { this.check.set(null); this.reviewed = false; this.error.set(''); }
  loadHistory() {
    this.historyLoading.set(true); this.historyError.set(false);
    this.service.history(this.account().id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.history.set(rows); this.historyLoading.set(false); },
      error: () => { this.historyError.set(true); this.historyLoading.set(false); },
    });
  }
  checkBalance() {
    if (this.busy() || this.statementBalance === null || !this.statementDate) return;
    this.changed(); this.busy.set(true);
    this.service.check(this.account().id, this.statementDate, this.statementBalance)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => { this.check.set(result); this.busy.set(false); },
        error: response => { this.busy.set(false); this.error.set(this.message(response, 'The balance couldn’t be checked. Try again.')); },
      });
  }
  confirm() {
    const check = this.check();
    if (this.busy() || !check?.matches || !this.reviewed) return;
    this.busy.set(true); this.error.set('');
    this.service.confirm(this.account().id, check).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.saved.emit(); },
      error: response => {
        this.busy.set(false); this.check.set(null); this.reviewed = false;
        this.error.set(this.message(response, 'The checkpoint couldn’t be saved. Check the balance again before confirming.'));
      },
    });
  }
}
