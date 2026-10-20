import { Component, DestroyRef, inject, Input, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TransferService } from '../../../core/services/transfer.service';
import { notificationSignal } from '../../../core/services/toast.service';
import { Account } from '../../../shared/models/account/account';
import { Transaction } from '../../../shared/models/transactions/transaction';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-transfer-reconciliation',
  imports: [CurrencyPipe, DatePipe, FormsModule, SelectMenu],
  templateUrl: './transfer-reconciliation.html', styleUrl: './transfer-reconciliation.css',
})
export class TransferReconciliation {
  @Input({ required: true }) transaction!: Transaction;
  @Input({ required: true }) accounts: Account[] = [];
  readonly completed = output<void>();
  readonly cancelled = output<void>();
  readonly staleDetected = output<void>();
  readonly busyChange = output<boolean>();
  private readonly api = inject(TransferService);
  private readonly destroy = inject(DestroyRef);
  private searchRequest?: Subscription;
  readonly candidates = signal<Transaction[]>([]);
  readonly searching = signal(false);
  readonly searched = signal(false);
  readonly busy = signal(false);
  readonly stale = signal(false);
  readonly error = notificationSignal('error');
  readonly mode = signal<'existing' | 'create'>('existing');
  readonly selected = signal<Transaction | null>(null);
  accountId = 0;
  days = 7;
  date = '';
  confirmed = false;
  readonly windows: SelectOption[] = [
    { value: 0, label: 'Same posting date' }, { value: 7, label: 'Within 7 days' },
    { value: 30, label: 'Within 30 days' }, { value: 365, label: 'Within 1 year' },
  ];
  get accountOptions(): SelectOption[] { return [{ value: 0, label: 'Select other account' }, ...this.accounts.filter(a => a.id !== this.transaction.accountId).map(a => ({ value: a.id, label: a.name, detail: a.isActive ? a.institutionName ?? 'No institution' : 'Closed account' }))]; }
  get destination() { return this.accounts.find(a => a.id === this.accountId); }
  search() {
    if (this.busy() || this.stale()) return;
    this.searchRequest?.unsubscribe(); this.selected.set(null); this.confirmed = false;
    this.candidates.set([]); this.searched.set(false); this.searching.set(false); this.error.set('');
    this.date = this.transaction.transactionDate.slice(0, 10);
    if (!this.accountId) return;
    this.searching.set(true);
    this.searchRequest = this.api.candidates(this.transaction.id, this.accountId, this.days).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: rows => { this.candidates.set(rows); this.searched.set(true); this.searching.set(false); },
      error: response => { this.searching.set(false); this.failed(response); },
    });
  }
  chooseMode(mode: 'existing' | 'create') { if (this.busy()) return; this.mode.set(mode); this.confirmed = false; }
  link() {
    const candidate = this.selected();
    if (!candidate || !this.searched() || this.searching() || this.busy() || this.stale()) return;
    this.setBusy(true); this.error.set('');
    this.api.link(this.transaction.id, candidate.id, this.transaction.editVersion ?? 0, candidate.editVersion ?? 0)
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: () => { this.setBusy(false); this.completed.emit(); }, error: response => this.failed(response) });
  }
  create() {
    if (!this.confirmed || !this.searched() || this.searching() || this.busy() || this.stale() || !this.destination?.isActive || !this.date || this.date < this.destination.openingBalanceDate) return;
    this.setBusy(true); this.error.set('');
    this.api.createCounterpart(this.transaction.id, this.accountId, this.date, this.transaction.editVersion ?? 0)
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: () => { this.setBusy(false); this.completed.emit(); }, error: response => this.failed(response) });
  }
  private setBusy(value: boolean) { this.busy.set(value); this.busyChange.emit(value); }
  private failed(response: { status?: number; error?: { detail?: string } }) {
    this.setBusy(false); this.stale.set(response.status === 404 || response.status === 409);
    if (this.stale()) this.staleDetected.emit();
    this.error.set(response.error?.detail ?? 'This transfer couldn’t be reconciled. Please try again.');
  }
}
