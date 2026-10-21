import { ToastService, notificationSignal } from '../../core/services/toast.service';
import { Component, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BudgetService } from '../../core/services/budget.service';
import { BillPaymentState, BillTransactionCandidate, BudgetEvent } from '../../shared/models/budget/budget';
import { Modal } from '../../shared/components/modal/modal';
import { getTrendDates } from '../dashboard/trend-range';

@Component({
  selector: 'app-bill-payment', imports: [Modal, FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './bill-payment.html', styleUrl: './bill-payment.css',
})
export class BillPayment implements OnInit {
  get contribution() { return ['Savings', 'Investments'].includes(this.bill.allocationType ?? ''); }
  get itemLabel() { return this.contribution ? 'contribution' : 'bill'; }
  private readonly toasts = inject(ToastService);
  @Input({ required: true }) bill!: BudgetEvent;
  @Input() startLinking = false;
  readonly dismiss = output<void>();
  readonly saved = output<BillPaymentState>();
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly error = notificationSignal('error');
  readonly candidates = signal<BillTransactionCandidate[]>([]);
  readonly today = getTrendDates('1D').endDate;
  isPaid = true; paidOn = this.today; note = ''; transactionId: number | null = null;
  search = ''; linking = false;
  linkedTransaction: BillPaymentState['linkedTransaction'] = null;
  private searchVersion = 0;
  ngOnInit() {
    this.isPaid = this.bill.payment?.isPaid ?? true;
    this.paidOn = this.bill.payment?.paidOn ?? this.today;
    this.note = this.bill.payment?.note ?? '';
    this.transactionId = this.bill.payment?.transactionId ?? null;
    this.linkedTransaction = this.bill.payment?.linkedTransaction ?? null;
    if (this.startLinking && this.transactionId === null) this.find();
  }
  find() {
    this.linking = true; this.loading.set(true); this.error.set('');
    const version = ++this.searchVersion;
    this.api.billTransactions(this.bill, this.search).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: rows => { if (version === this.searchVersion) { this.candidates.set(rows); this.loading.set(false); } },
      error: () => { if (version === this.searchVersion) { this.loading.set(false); this.error.set('Transactions couldn’t load. Try searching again.'); } },
    });
  }
  choose(entry: BillTransactionCandidate) { this.linkedTransaction = entry; this.transactionId = entry.id; this.isPaid = true; if (!this.bill.payment?.isPaid) this.paidOn = entry.date; this.linking = false; }
  unpaid() { this.isPaid = false; this.transactionId = null; }
  save() {
    if (this.busy() || (this.isPaid && (!this.paidOn || this.paidOn > this.today)) || this.note.length > 500) return;
    this.busy.set(true); this.error.set('');
    this.api.saveBillPayment(this.bill, { isPaid: this.isPaid, paidOn: this.isPaid ? this.paidOn : null, note: this.note || null, transactionId: this.transactionId })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: result => { this.toasts.success(this.contribution ? 'Contribution payment updated.' : 'Bill payment updated.'); this.busy.set(false); this.saved.emit(result); },
        error: error => { this.busy.set(false); this.error.set(error.error?.detail ?? 'Payment couldn’t save. Please try again.'); },
      });
  }
}
