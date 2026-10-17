import { Component, computed, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../../core/services/account.service';
import { CategoryService } from '../../../core/services/category.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { Account } from '../../../shared/models/account/account';
import { Category } from '../../../shared/models/category/category';
import { CreateTransactionRequest } from '../../../shared/models/transactions/create-transaction-request';
import { getTrendDates } from '../../dashboard/trend-range';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-create-transaction',
  imports: [FormsModule, SelectMenu],
  host: { '[class.embedded]': 'embedded' },
  templateUrl: './create-transaction.html',
  styleUrl: './create-transaction.css',
})
export class CreateTransaction implements OnInit {
  @Input() embedded = false;
  @Input() accountId: number | null = null;
  readonly saved = output<void>();
  readonly cancelled = output<void>();
  get accountOptions(): SelectOption[] {
    return [{ value: 0, label: 'Select account' }, ...this.accounts.map(a => ({ value: a.id, label: a.name, detail: a.institutionName ?? 'No institution' }))];
  }
  get categoryOptions(): SelectOption[] {
    return [{ value: 0, label: 'Select category' }, ...this.categories.map(c => ({ value: c.id, label: c.name, detail: c.categoryType }))];
  }
  private readonly accountService = inject(AccountService);
  private readonly categoryService = inject(CategoryService);
  private readonly transactionService = inject(TransactionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly categorySaving = signal(false);
  readonly busy = computed(() => this.saving() || this.categorySaving());
  readonly addingCategory = signal(false);
  readonly categoryError = signal('');
  categoryName = '';
  categoryType: 'Expense' | 'Income' = 'Expense';
  readonly categoryTypes: SelectOption[] = [
    { value: 'Expense', label: 'Expense' }, { value: 'Income', label: 'Income' },
  ];
  readonly error = signal('');
  accounts: Account[] = [];
  categories: Category[] = [];
  private readonly returnAccountId = Number(this.route.snapshot.queryParamMap.get('accountId')) || null;
  request: CreateTransactionRequest = {
    accountId: 0, categoryId: 0, amount: 0,
    transactionDate: getTrendDates('1D').endDate,
    description: '', merchant: null, notes: null, isCleared: false,
  };
  get openingDate(): string | null {
    return this.accounts.find(a => a.id === this.request.accountId)?.openingBalanceDate ?? null;
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
    if (form.invalid || this.request.accountId <= 0 || this.request.categoryId <= 0 ||
      this.request.amount <= 0 || !this.request.description.trim() || this.busy() || this.addingCategory() ||
      (this.openingDate && this.request.transactionDate < this.openingDate)) return;
    this.saving.set(true);
    this.error.set('');
    this.transactionService.CreateTransaction({ ...this.request, description: this.request.description.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.saving.set(false);
          if (this.embedded) this.saved.emit();
          else void this.router.navigate(['/accounts'], { queryParams: { accountId: this.request.accountId } });
        },
        error: response => {
          this.saving.set(false);
          this.error.set(typeof response.error === 'string' ? response.error : 'Your transaction couldn’t be saved. Check the details and try again.');
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
        next: category => {
          this.categories = [...this.categories, category].sort((a, b) => a.name.localeCompare(b.name));
          this.request.categoryId = category.id;
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
