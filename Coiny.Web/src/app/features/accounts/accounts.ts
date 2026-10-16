import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Subscription } from 'rxjs';
import { AccountService } from '../../core/services/account.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { Institution, InstitutionService } from '../../core/services/institution.service';
import { Account } from '../../shared/models/account/account';
import { Category } from '../../shared/models/category/category';
import { TransactionPage } from '../../shared/models/transactions/transaction-page';
import { TrendResult } from '../../shared/models/trends/trend-result';
import { TrendChart } from '../../shared/components/trend-chart/trend-chart';
import { Icon } from '../../shared/components/icon/icon';
import { TransactionList } from '../dashboard/transaction-list/transaction-list';
import { getTrendDates, TrendRange } from '../dashboard/trend-range';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';
import { Modal } from '../../shared/components/modal/modal';
import { CreateAccount } from './create-account/create-account';
import { CreateTransaction } from '../transactions/create-transaction/create-transaction';
import { CreateCategory } from './create-category/create-category';

@Component({
  selector: 'app-accounts',
  imports: [CurrencyPipe, DecimalPipe, FormsModule, TrendChart, Icon, TransactionList, SelectMenu, Modal, CreateAccount, CreateTransaction, CreateCategory],
  templateUrl: './accounts.html',
  styleUrl: './accounts.css',
})
export class Accounts implements OnInit {
  readonly modal = signal<'account' | 'transaction' | 'category' | null>(null);
  readonly notice = signal('');
  private readonly money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
  readonly accountOptions = computed<SelectOption[]>(() => [
    { value: 'all', label: 'All accounts', detail: 'Your household' },
    ...this.accounts().map(a => ({ value: String(a.id),
      label: a.name + ' · ' + this.money.format(a.currentBalance) + (a.isActive ? '' : ' (closed)'),
      detail: a.institutionName ?? 'No institution' })),
  ]);
  readonly typeOptions: SelectOption[] = [
    { value: '', label: 'All types' }, ...['Income', 'Expense', 'Transfer'].map(value => ({ value, label: value })),
  ];
  readonly categoryOptions = computed<SelectOption[]>(() => [
    { value: '', label: 'All categories' }, ...this.categories().map(c => ({ value: String(c.id), label: c.name })),
  ]);
  private readonly accountService = inject(AccountService);
  private readonly dashboardService = inject(DashboardService);
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly institutionService = inject(InstitutionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private trendRequest?: Subscription;
  private transactionRequest?: Subscription;
  private overviewRequest?: Subscription;

  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly institutions = signal<Institution[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly selectedId = signal<number | null>(null);
  readonly selectedAccount = computed(() => this.accounts().find(a => a.id === this.selectedId()) ?? null);
  readonly isDebt = computed(() => ['CreditCard', 'Loan'].includes(this.selectedAccount()?.accountType ?? ''));
  readonly balance = computed(() => this.selectedAccount()?.currentBalance ??
    this.accounts().reduce((sum, a) => sum + (['CreditCard', 'Loan'].includes(a.accountType) ? -a.currentBalance : a.currentBalance), 0));
  readonly institution = computed(() => this.institutions().find(i => i.id === this.selectedAccount()?.institutionId));
  readonly website = computed(() => {
    const raw = this.institution()?.website?.trim();
    if (!raw) return null;
    try {
      const url = new URL(/^[a-z][a-z0-9+.-]*:/i.test(raw) ? raw : 'https://' + raw);
      return ['http:', 'https:'].includes(url.protocol) ? url.href : null;
    } catch { return null; }
  });
  readonly ranges: TrendRange[] = ['1D', '1W', '1M', '3M', '6M', '1Y', 'YTD'];
  readonly range = signal<TrendRange>('1M');
  readonly dates = computed(() => getTrendDates(this.range()));
  readonly trend = signal<TrendResult | null>(null);
  readonly trendLoading = signal(true);
  readonly trendError = signal(false);
  readonly favorable = computed(() => {
    const change = this.trend()?.absoluteChange ?? 0;
    return this.isDebt() ? change < 0 : change > 0;
  });
  readonly magnitude = Math.abs;
  readonly filterOpen = signal(false);
  search = '';
  categoryId = '';
  categoryType = '';
  private appliedFilters = { search: '', categoryId: '', categoryType: '' };
  readonly page = signal<TransactionPage | null>(null);
  readonly transactionLoading = signal(true);
  readonly transactionError = signal(false);
  readonly currentPage = signal(1);
  readonly totalPages = computed(() => Math.max(1, Math.ceil((this.page()?.totalCount ?? 0) / 10)));
  readonly pageNumbers = computed(() => {
    const first = Math.max(1, Math.min(this.currentPage() - 2, this.totalPages() - 4));
    return Array.from({ length: Math.min(5, this.totalPages()) }, (_, i) => first + i);
  });
  readonly firstResult = computed(() => this.page()?.totalCount ? (this.currentPage() - 1) * 10 + 1 : 0);
  readonly lastResult = computed(() => this.firstResult() ? this.firstResult() + (this.page()?.items.length ?? 0) - 1 : 0);
  readonly hasFilters = signal(false);

  ngOnInit(): void {
    const requestedRange = this.route.snapshot.queryParamMap.get('range') as TrendRange;
    if (this.ranges.includes(requestedRange)) this.range.set(requestedRange);
    const id = Number(this.route.snapshot.queryParamMap.get('accountId'));
    if (Number.isInteger(id) && id > 0) this.selectedId.set(id);
    const create = this.route.snapshot.queryParamMap.get('create');
    if (create === 'account' || create === 'transaction') this.modal.set(create);
    this.loadOverview();
  }

  loadOverview(refresh = false): void {
    this.overviewRequest?.unsubscribe();
    this.loading.set(!refresh);
    this.loadError.set(false);
    this.overviewRequest = forkJoin({
      accounts: this.accountService.getAccounts(),
      categories: this.categoryService.getCategories(),
      institutions: this.institutionService.getInstitutions(),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => {
        this.accounts.set(data.accounts);
        this.categories.set(data.categories);
        this.institutions.set(data.institutions);
        if (!data.accounts.some(a => a.id === this.selectedId())) this.selectedId.set(null);
        this.loading.set(false);
        this.loadTrend();
        this.loadTransactions(1);
      },
      error: () => { this.loading.set(false); this.loadError.set(true); },
    });
  }

  selectAccount(value: string): void {
    this.selectedId.set(value === 'all' ? null : Number(value));
    this.syncUrl();
    this.loadTrend();
    this.loadTransactions(1);
  }

  openModal(kind: 'account' | 'transaction' | 'category'): void { this.notice.set(''); this.modal.set(kind); }
  closeModal(): void { this.modal.set(null); this.syncUrl(); }
  accountCreated(account: Account): void {
    this.selectedId.set(account.id);
    this.closeModal();
    this.notice.set('Account added.');
    this.loadOverview(true);
  }
  transactionCreated(): void {
    this.closeModal();
    this.notice.set('Transaction added.');
    this.loadOverview(true);
  }
  categoryCreated(category: Category): void {
    this.categories.update(categories => [...categories.filter(c => c.id !== category.id), category]
      .sort((a, b) => a.categoryType.localeCompare(b.categoryType) || a.name.localeCompare(b.name)));
    this.closeModal();
    this.notice.set('Category added. It’s now available across your accounts.');
  }

  selectRange(value: TrendRange): void {
    if (this.range() === value) return;
    this.range.set(value);
    this.syncUrl();
    this.loadTrend();
    this.loadTransactions(1);
  }

  private syncUrl(): void {
    void this.router.navigate([], { relativeTo: this.route, replaceUrl: true,
      queryParams: { accountId: this.selectedId(), range: this.range() } });
  }

  loadTrend(): void {
    this.trendRequest?.unsubscribe();
    this.trend.set(null);
    this.trendLoading.set(true);
    this.trendError.set(false);
    const { startDate, endDate } = this.dates();
    const id = this.selectedId();
    const request = id === null
      ? this.dashboardService.getNetWorthTrend(startDate, endDate)
      : this.accountService.getTrend(id, startDate, endDate);
    this.trendRequest = request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => { this.trend.set(result); this.trendLoading.set(false); },
      error: () => { this.trendError.set(true); this.trendLoading.set(false); },
    });
  }

  applyFilters(): void {
    this.appliedFilters = { search: this.search.trim(), categoryId: this.categoryId, categoryType: this.categoryType };
    this.hasFilters.set(Object.values(this.appliedFilters).some(Boolean));
    this.loadTransactions(1);
  }

  clearFilters(): void {
    this.search = this.categoryId = this.categoryType = '';
    this.applyFilters();
  }

  loadTransactions(page = this.currentPage()): void {
    this.transactionRequest?.unsubscribe();
    this.currentPage.set(page);
    this.page.set(null);
    this.transactionLoading.set(true);
    this.transactionError.set(false);
    const filters = this.appliedFilters;
    this.transactionRequest = this.transactionService.getPage({
      ...this.dates(), accountId: this.selectedId() ?? undefined,
      categoryId: filters.categoryId ? Number(filters.categoryId) : undefined,
      categoryType: filters.categoryType || undefined, search: filters.search || undefined,
      page, pageSize: 10,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.page.set(result);
        this.currentPage.set(result.page);
        this.transactionLoading.set(false);
      },
      error: () => { this.transactionError.set(true); this.transactionLoading.set(false); },
    });
  }
}
