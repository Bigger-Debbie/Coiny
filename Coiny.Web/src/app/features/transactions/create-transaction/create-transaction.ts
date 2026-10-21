import { ToastService, notificationSignal } from '../../../core/services/toast.service';
import { Component, computed, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Subscription } from 'rxjs';
import { AccountService } from '../../../core/services/account.service';
import { CategoryService } from '../../../core/services/category.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { TransferService } from '../../../core/services/transfer.service';
import { Observable } from 'rxjs';
import { Account } from '../../../shared/models/account/account';
import { Category } from '../../../shared/models/category/category';
import { CreateTransactionRequest } from '../../../shared/models/transactions/create-transaction-request';
import { Transaction } from '../../../shared/models/transactions/transaction';
import { CreateTransfer } from '../../../shared/models/transactions/transfer';
import { getTrendDates } from '../../dashboard/trend-range';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-create-transaction',
  imports: [FormsModule, SelectMenu, CurrencyPipe, DatePipe],
  host: { '[class.embedded]': 'embedded' },
  templateUrl: './create-transaction.html',
  styleUrl: './create-transaction.css',
})
export class CreateTransaction implements OnInit {
  private readonly toasts = inject(ToastService);
  @Input() embedded = false;
  @Input() accountId: number | null = null;
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly transferMatches = signal<Transaction[]>([]);
  readonly matchLoading = signal(false);
  readonly matchError = signal(false);
  transferChoice: number | 'both' | null = null;
  private checkedTransferKey = '';
  private matchRequest?: Subscription;
  private matchTimer?: ReturnType<typeof setTimeout>;
  get internalTransfer() { return this.kind === 'Transfer' && this.transferMode === 'Internal'; }
  private get transferKey() { return JSON.stringify([this.request.accountId, this.toAccountId, this.request.amount, this.request.transactionDate]); }
  get transferChecked() { return this.checkedTransferKey === this.transferKey && !this.matchLoading() && !this.matchError(); }
  get selectedTransferMatch() { return this.transferMatches().find(t => t.id === this.transferChoice); }
  get matchesInBothAccounts() { return new Set(this.transferMatches().map(t => t.accountId)).size > 1; }
  get missingAccountName() {
    const match = this.selectedTransferMatch;
    return this.accounts.find(a => a.id === (match?.accountId === this.request.accountId ? this.toAccountId : this.request.accountId))?.name;
  }
  constructor() { this.destroyRef.onDestroy(() => clearTimeout(this.matchTimer)); }
  transferInputsChanged() {
    clearTimeout(this.matchTimer); this.matchRequest?.unsubscribe();
    this.checkedTransferKey = ''; this.transferChoice = null; this.transferMatches.set([]);
    this.matchError.set(false); this.matchLoading.set(false);
    if (this.internalTransfer && this.canCheckTransfer())
      this.matchTimer = setTimeout(() => this.checkTransferMatches(), 300);
  }
  private canCheckTransfer() {
    return this.request.accountId > 0 && this.toAccountId > 0 && this.request.accountId !== this.toAccountId &&
      Number.isFinite(this.request.amount) && this.request.amount > 0 &&
      Math.abs(this.request.amount * 100 - Math.round(this.request.amount * 100)) < .000001 && !!this.request.transactionDate;
  }
  private transferRequest(): CreateTransfer {
    return { fromAccountId: this.request.accountId, toAccountId: this.toAccountId,
      amount: this.request.amount, date: this.request.transactionDate,
      description: this.request.description.trim() || null, notes: this.request.notes, isCleared: this.request.isCleared };
  }
  checkTransferMatches() {
    clearTimeout(this.matchTimer); this.matchRequest?.unsubscribe();
    this.transferChoice = null; this.checkedTransferKey = ''; this.transferMatches.set([]);
    if (!this.internalTransfer || !this.canCheckTransfer()) return;
    const key = this.transferKey;
    this.matchLoading.set(true); this.matchError.set(false);
    this.matchRequest = this.transferService.creationCandidates(this.transferRequest()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => {
        this.matchLoading.set(false);
        if (key !== this.transferKey) return;
        this.transferMatches.set(rows); this.checkedTransferKey = key;
        if (!rows.length) this.transferChoice = 'both';
      },
      error: () => { this.matchLoading.set(false); this.matchError.set(true); },
    });
  }
  get accountOptions(): SelectOption[] {
    return [{ value: 0, label: 'Select account' }, ...this.accounts.map(a => ({ value: a.id, label: a.name, detail: a.institutionName ?? 'No institution' }))];
  }
  get categoryOptions(): SelectOption[] {
    return [{ value: 0, label: 'Select category' }, ...this.categories.filter(c => c.categoryType === this.kind).map(c => ({ value: c.id, label: c.name, detail: c.categoryType }))];
  }
  kind: 'Expense' | 'Income' | 'Transfer' = 'Expense';
  transferMode: 'Internal' | 'Unknown' | 'External' = 'Internal';
  toAccountId = 0;
  direction: 'In' | 'Out' = 'Out';
  readonly kinds: SelectOption[] = ['Expense', 'Income', 'Transfer'].map(value => ({ value, label: value }));
  readonly transferModes: SelectOption[] = [
    { value: 'Internal', label: 'Between Coiny accounts' },
    { value: 'External', label: 'Account outside Coiny' },
    { value: 'Unknown', label: 'Other account unknown — link later' },
  ];
  readonly directions: SelectOption[] = [{ value: 'Out', label: 'Money out' }, { value: 'In', label: 'Money in' }];
  get destinationOptions(): SelectOption[] { return this.accountOptions.filter(a => a.value !== this.request.accountId); }
  changeKind(kind: 'Expense' | 'Income' | 'Transfer'): void {
    this.kind = kind;
    this.request.categoryId = 0;
    this.closeCategory();
    if (kind !== 'Transfer') this.categoryType = kind;
    this.error.set('');
    this.transferInputsChanged();
  }
  private readonly accountService = inject(AccountService);
  private readonly categoryService = inject(CategoryService);
  private readonly transactionService = inject(TransactionService);
  private readonly transferService = inject(TransferService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly categorySaving = signal(false);
  readonly busy = computed(() => this.saving() || this.categorySaving());
  readonly addingCategory = signal(false);
  readonly categoryError = notificationSignal('error');
  categoryName = '';
  categoryType: 'Expense' | 'Income' = 'Expense';
  readonly categoryTypes: SelectOption[] = [
    { value: 'Expense', label: 'Expense' }, { value: 'Income', label: 'Income' },
  ];
  readonly error = notificationSignal('error');
  accounts: Account[] = [];
  categories: Category[] = [];
  private readonly returnAccountId = Number(this.route.snapshot.queryParamMap.get('accountId')) || null;
  request: CreateTransactionRequest = {
    accountId: 0, categoryId: 0, amount: 0,
    transactionDate: getTrendDates('1D').endDate,
    description: '', merchant: null, notes: null, isCleared: false,
  };
  get openingDate(): string | null {
    const match = this.internalTransfer ? this.selectedTransferMatch : null;
    if (match) return this.accounts.find(a => a.id === (match.accountId === this.request.accountId ? this.toAccountId : this.request.accountId))?.openingBalanceDate ?? null;
    const dates = this.accounts.filter(a => a.id === this.request.accountId ||
      (this.kind === 'Transfer' && this.transferMode === 'Internal' && a.id === this.toAccountId))
      .map(a => a.openingBalanceDate).sort();
    return dates.at(-1) ?? null;
  }

  ngOnInit(): void { this.loadOptions(); }
  loadOptions(): void {
    this.loading.set(true);
    this.loadError.set(false);
    forkJoin({ accounts: this.accountService.getAccounts(), categories: this.categoryService.getCategories() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => {
          this.accounts = data.accounts.filter(a => a.isActive);
          this.categories = data.categories.filter(c => c.isActive && c.categoryType !== 'Transfer');
          const selected = this.embedded ? this.accountId : this.returnAccountId;
          if (this.accounts.some(a => a.id === selected)) this.request.accountId = selected!;
          this.loading.set(false);
        },
        error: () => { this.loading.set(false); this.loadError.set(true); },
      });
  }

  save(form: NgForm): void {
    if (this.busy() || this.addingCategory()) return;
    const transfer = this.kind === 'Transfer';
    if (form.invalid || !this.accounts.some(a => a.id === this.request.accountId) || this.request.amount <= 0 ||
      !Number.isFinite(this.request.amount) || Math.abs(this.request.amount * 100 - Math.round(this.request.amount * 100)) > 0.000001 ||
      (!transfer && (!this.request.description.trim() || !this.categories.some(c => c.id === this.request.categoryId && c.categoryType === this.kind))) ||
      (transfer && this.transferMode === 'Internal' && (this.toAccountId === this.request.accountId || !this.accounts.some(a => a.id === this.toAccountId))) ||
      !this.request.transactionDate || (this.openingDate && this.request.transactionDate < this.openingDate)) {
      this.error.set('Check the accounts, category, amount (up to two decimal places), and date. The date must be on or after the opening date of each account.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    if (this.internalTransfer && (!this.transferChecked || this.transferChoice === null)) {
      this.saving.set(false);
      this.error.set('Check for existing transfers and choose how to record this transfer before saving.');
      if (!this.transferChecked && !this.matchLoading()) this.checkTransferMatches();
      return;
    }
    const description = this.request.description.trim();
    let operation: Observable<unknown>;
    if (!transfer) operation = this.transactionService.CreateTransaction({ ...this.request, description });
    else if (this.transferMode === 'Internal') {
      const match = this.selectedTransferMatch;
      operation = this.transferService.create({ ...this.transferRequest(),
        ...(match ? { existingTransactionId: match.id, expectedExistingEditVersion: match.editVersion }
          : { createBothEntries: this.transferMatches().length > 0 }) });
    }
    else operation = this.transferService.createEntry({
      accountId: this.request.accountId, direction: this.direction, counterparty: this.transferMode,
      amount: this.request.amount, date: this.request.transactionDate,
      description: description || 'Transfer', notes: this.request.notes, isCleared: this.request.isCleared,
    });
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.saving.set(false);
          if (!this.embedded) this.toasts.success('Transaction created.');
          if (this.embedded) this.saved.emit();
          else void this.router.navigate(['/accounts'], { queryParams: { accountId: this.request.accountId } });
        },
        error: response => {
          this.saving.set(false);
          if (this.internalTransfer) {
            this.checkedTransferKey = ''; this.transferChoice = null; this.transferMatches.set([]);
          }
          this.error.set(typeof response.error === 'string' ? response.error : response.error?.detail || 'Your transaction couldn’t be saved. Check the details and try again.');
        },
      });
  }

  cancel(): void {
    if (this.busy()) return;
    if (this.embedded) this.cancelled.emit();
    else void this.router.navigate(['/accounts'], { queryParams: { accountId: this.returnAccountId } });
  }
  closeCategory(): void {
    if (this.busy()) return;
    this.addingCategory.set(false);
    this.categoryName = '';
    this.categoryType = 'Expense';
    this.categoryError.set('');
  }
  saveCategory(): void {
    if (this.busy()) return;
    const name = this.categoryName.trim();
    if (!name || name.length > 100) {
      this.categoryError.set('Enter a category name of up to 100 characters.'); return;
    }
    this.categorySaving.set(true);
    this.categoryError.set('');
    this.categoryService.createCategory({ name, categoryType: this.categoryType })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: category => { this.toasts.success('Category created.');
          this.categories = [...this.categories, category].sort((a, b) => a.name.localeCompare(b.name));
          this.request.categoryId = category.id;
          this.kind = category.categoryType as 'Income' | 'Expense';
          this.categorySaving.set(false);
          this.closeCategory();
        },
        error: response => {
          this.categorySaving.set(false);
          this.categoryError.set(typeof response.error === 'string'
            ? response.error : 'Your category couldn’t be saved. Please try again.');
        },
      });
  }
}
