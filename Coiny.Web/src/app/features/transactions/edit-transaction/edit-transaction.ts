import { notificationSignal } from '../../../core/services/toast.service';
import { afterNextRender, ElementRef, Injector, Component, DestroyRef, inject, Input, HostListener, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountService } from '../../../core/services/account.service';
import { CategoryService } from '../../../core/services/category.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { Account } from '../../../shared/models/account/account';
import { Category } from '../../../shared/models/category/category';
import { TransactionEditDetails, UpdateTransactionRequest } from '../../../shared/models/transactions/transaction-management';
import { Modal } from '../../../shared/components/modal/modal';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-edit-transaction', imports: [FormsModule, CurrencyPipe, DatePipe, Modal, SelectMenu],
  templateUrl: './edit-transaction.html', styleUrl: './edit-transaction.css',
})
export class EditTransaction implements OnInit {
  @Input({ required: true }) transactionId!: number;
  readonly dismiss = output<void>();
  readonly changed = output<'updated' | 'deleted'>();
  private readonly api = inject(TransactionService);
  private readonly accountApi = inject(AccountService);
  private readonly categoryApi = inject(CategoryService);
  private readonly destroy = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = notificationSignal('error');
  readonly stale = signal(false);
  readonly deleting = signal(false);
  readonly editing = signal(false);
  details: TransactionEditDetails | null = null;
  request: UpdateTransactionRequest | null = null;
  accounts: Account[] = []; categories: Category[] = [];
  private original = '';
  readonly directions: SelectOption[] = [{ value: 'Out', label: 'Money out' }, { value: 'In', label: 'Money in' }];
  readonly counterparties: SelectOption[] = [{ value: 'Unknown', label: 'Other account unknown — link later' }, { value: 'External', label: 'Account outside Coiny' }];
  get linked() { return this.details?.transaction.pairedTransactionId != null; }
  get kinds(): SelectOption[] { return (this.linked ? ['Transfer'] : ['Expense', 'Income', 'Transfer']).map(value => ({ value, label: value })); }
  get accountOptions(): SelectOption[] {
    return this.accounts.filter(a => (a.isActive || a.id === this.details?.transaction.accountId) && a.id !== this.details?.transaction.counterpartyAccountId)
      .map(a => ({ value: a.id, label: a.name, detail: a.institutionName ?? 'No institution' }));
  }
  get categoryOptions(): SelectOption[] {
    return [{ value: 0, label: 'Select category' }, ...this.categories.filter(c => c.categoryType === this.request?.categoryType && (c.isActive || c.id === this.details?.transaction.categoryId)).map(c => ({ value: c.id, label: c.name }))];
  }
  get openingDate() { return this.accounts.find(a => a.id === this.request?.accountId)?.openingBalanceDate ?? null; }
  get dirty() { return !!this.request && JSON.stringify(this.request) !== this.original; }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) { if (this.busy() || this.dirty) event.preventDefault(); }
  ngOnInit() { this.load(); }
  load() {
    if (this.busy() || (this.dirty && !window.confirm('Reload this transaction and discard your unsaved changes?'))) return;
    this.loading.set(true); this.error.set(''); this.deleting.set(false);
    forkJoin({ details: this.api.getDetails(this.transactionId), accounts: this.accountApi.getAccounts(), categories: this.categoryApi.getCategories() })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: ({ details, accounts, categories }) => {
          this.details = details; this.accounts = accounts; this.categories = categories;
          const t = details.transaction;
          this.request = { expectedEditVersion: t.editVersion ?? 0, expectedPairedEditVersion: details.pairedEditVersion,
            accountId: t.accountId, categoryType: t.categoryType as UpdateTransactionRequest['categoryType'], categoryId: t.categoryId,
            amount: t.amount, date: t.transactionDate.slice(0, 10), description: t.description, merchant: t.merchant,
            notes: t.notes ?? null, isCleared: t.isCleared, direction: t.transferDirection ?? null, counterparty: t.transferCounterparty ?? null };
          this.original = JSON.stringify(this.request); this.stale.set(false); this.loading.set(false);
        },
        error: response => { this.loading.set(false); this.error.set(response.error?.detail ?? 'This transaction couldn’t load. Please try again.'); },
      });
  }
  startEditing() { if (!this.busy() && !this.stale()) { this.editing.set(true); this.focusAfterRender('#edit-kind'); } }
  cancelEditing() {
    if (this.busy() || (this.dirty && !window.confirm('Discard your unsaved transaction changes?'))) return;
    this.request = JSON.parse(this.original) as UpdateTransactionRequest;
    this.editing.set(false); this.deleting.set(false); if (!this.stale()) this.error.set('');
    this.focusAfterRender('[data-edit-transaction]');
  }
  private focusAfterRender(selector: string) {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), { injector: this.injector });
  }
  changeKind(kind: UpdateTransactionRequest['categoryType']) {
    if (!this.request || this.linked) return;
    this.request.categoryType = kind; this.request.categoryId = 0;
    this.request.direction = kind === 'Transfer' ? 'Out' : null;
    this.request.counterparty = kind === 'Transfer' ? 'Unknown' : null;
  }
  save(form: NgForm) {
    const r = this.request;
    if (!r || this.busy() || this.stale()) return;
    if (form.invalid || !r.date || (this.openingDate && r.date < this.openingDate) || !r.description.trim() ||
        !Number.isFinite(r.amount) || r.amount <= 0 || Math.abs(r.amount * 100 - Math.round(r.amount * 100)) > .000001 ||
        (r.categoryType !== 'Transfer' && !this.categories.some(c => c.id === r.categoryId && c.categoryType === r.categoryType))) {
      this.error.set('Check the account, category, date, description, and amount with up to two decimal places.'); return;
    }
    this.busy.set(true); this.error.set('');
    this.api.update(this.transactionId, { ...r, description: r.description.trim() }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => { this.busy.set(false); this.changed.emit('updated'); }, error: response => this.fail(response),
    });
  }
  remove() {
    if (!this.details || this.busy() || this.stale()) return;
    this.busy.set(true); this.error.set('');
    this.api.delete(this.transactionId, this.details.transaction.editVersion ?? 0, this.details.pairedEditVersion).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => { this.busy.set(false); this.changed.emit('deleted'); }, error: response => this.fail(response),
    });
  }
  close() { if (!this.busy() && (!this.dirty || window.confirm('Discard your unsaved transaction changes?'))) this.dismiss.emit(); }
  private fail(response: { status?: number; error?: { detail?: string } }) {
    this.busy.set(false); this.stale.set(response.status === 409 || response.status === 404);
    this.error.set(response.error?.detail ?? 'Your changes couldn’t be saved. Please try again.');
  }
}
