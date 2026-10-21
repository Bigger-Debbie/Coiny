import { ColumnResize } from '../../../shared/components/column-resize/column-resize';
import { ToastService, notificationSignal } from '../../../core/services/toast.service';
import { Component, computed, DestroyRef, inject, Input, OnChanges, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CategoryService } from '../../../core/services/category.service';
import { TransactionImportService } from '../../../core/services/transaction-import.service';
import { Account } from '../../../shared/models/account/account';
import { Category } from '../../../shared/models/category/category';
import { BankBalanceReference, ImportRowCheck, TransactionImportRequest, TransactionImportResult, TransactionImportValidation } from '../../../shared/models/transactions/transaction-import';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';
import { ImportReviewRow } from './csv-mapping';
import { ReviewCell } from './review-cell';

interface ReviewRow extends ImportReviewRow {
  selected: boolean; categoryChoice: string | null; counterparty: 'Unknown' | 'External'; allowDuplicate: boolean;
}

@Component({ selector: 'app-import-review', host: { 'data-review-surface': '' }, imports: [FormsModule, CurrencyPipe, DatePipe, RouterLink, SelectMenu, ReviewCell, ColumnResize],
  templateUrl: './import-review.html', styleUrls: ['./import-transactions.css', './import-review.css'] })
export class ImportReview implements OnChanges {
  readonly columns = [
    {label:'Row',width:80,min:72,max:160}, {label:'Include',width:64,min:64,max:120},
    {label:'Date',width:120,min:112,max:200}, {label:'Description',width:320,min:160,max:720},
    {label:'Merchant',width:160,min:112,max:480}, {label:'Direction',width:112,min:104,max:200},
    {label:'Amount',width:112,min:104,max:240}, {label:'Category',width:240,min:160,max:480},
    {label:'Other account',width:184,min:144,max:480}, {label:'Bank classification',width:160,min:128,max:400},
    {label:'Status',width:144,min:128,max:240},
  ];
  readonly columnWidths = signal(this.columns.map(column => column.width));
  readonly tableWidth = computed(() => this.columnWidths().reduce((sum,width) => sum+width,0));
  resizeColumn(index:number,width:number) {
    const column=this.columns[index]; if(!column || !Number.isFinite(width)) return;
    this.columnWidths.update(widths=>widths.map((value,i)=>i===index?Math.round(Math.max(column.min,Math.min(column.max,width))):value));
  }
  resetColumnWidths() { this.columnWidths.set(this.columns.map(column=>column.width)); }
  private readonly toasts = inject(ToastService);
  @Input({ required: true }) mappedRows: readonly ImportReviewRow[] = [];
  @Input({ required: true }) account!: Account;
  @Input() sessionMode = false;
  readonly reviewed = output<TransactionImportRequest>();
  readonly reviewInvalidated = output<void>();
  @Input() bankBalance: BankBalanceReference | null = null;
  @Input({ required: true }) filename = '';
  readonly busyChange = output<boolean>();
  readonly completed = output<void>();
  readonly lockedChange = output<boolean>();
  private readonly categoriesApi = inject(CategoryService);
  private readonly api = inject(TransactionImportService);
  private readonly destroy = inject(DestroyRef);
  readonly rows = signal<ReviewRow[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly history = signal<TransactionImportResult[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly busy = signal(false);
  readonly validation = signal<TransactionImportValidation | null>(null);
  private readonly lastChecks = signal<ImportRowCheck[]>([]);
  readonly result = signal<TransactionImportResult | null>(null);
  readonly error = notificationSignal('error');
  readonly notice = notificationSignal('success');
  readonly locked = signal(false);
  readonly creatingCategory = signal(false);
  readonly detailLine = signal<number | null>(null);
  get detailRow(): ReviewRow | undefined { return this.rows().find(row => row.line === this.detailLine()); }
  categoryLabel(row: ReviewRow): string { return this.options(row).find(option => option.value === row.categoryChoice)?.label ?? 'Choose category'; }
  status(row: ReviewRow): 'excluded' | 'error' | 'review' | 'ready' {
    if (row.excluded) return 'excluded';
    if (this.localErrors(row).length || this.serverErrors(row).some(error => !/categor|duplicate|assign/i.test(error))) return 'error';
    if (!row.categoryChoice || this.duplicate(row) && !row.allowDuplicate || this.serverErrors(row).length) return 'review';
    return 'ready';
  }
  statusLabel(row: ReviewRow): string {
    switch (this.status(row)) {
      case 'excluded': return 'Excluded';
      case 'error': return 'Fix data';
      case 'review': return !row.categoryChoice ? 'Needs category' : this.duplicate(row) && !row.allowDuplicate ? 'Possible duplicate' : 'Needs review';
      default: return this.validation() ? 'Ready' : 'Ready to check';
    }
  }
  edit(row: ReviewRow, field: 'date' | 'description' | 'merchant' | 'direction' | 'amount' | 'categoryChoice' | 'counterparty', value: string | number | null): void {
    if (this.busy() || this.locked()) return;
    if (['date', 'description', 'direction', 'amount'].includes(field)) row.allowDuplicate = false;
    switch (field) {
      case 'date': row.date = value === null ? null : String(value); break;
      case 'description': row.description = String(value ?? ''); break;
      case 'merchant': row.merchant = value ? String(value) : null; break;
      case 'amount': row.amount = value === null ? null : Number(value); break;
      case 'direction': row.direction = value as 'In' | 'Out'; this.directionChanged(row); return;
      case 'categoryChoice': row.categoryChoice = value as string | null; break;
      case 'counterparty': row.counterparty = value as 'Unknown' | 'External'; break;
    }
    this.changed(['merchant', 'categoryChoice', 'counterparty'].includes(field));
  }
  search = ''; classification = ''; page = 1;
  bulkChoice: string | null = null;
  balanceAcknowledged = false;
  get canImportReviewed(): boolean {
    const checked = this.validation();
    return !!checked && (checked.canImport || (!!checked.balanceComparison?.requiresAcknowledgment && this.balanceAcknowledged && checked.rows.every(row => !row.errors.length)));
  }
  finalConfirmed = false;
  cleared = true;
  categoryName = ''; categoryType: 'Expense' | 'Income' = 'Expense';
  private submissionId = crypto.randomUUID();
  private pendingRequest: TransactionImportRequest | null = null;
  readonly directions: SelectOption[] = [{ value: 'In', label: 'Money in' }, { value: 'Out', label: 'Money out' }];
  readonly counterparties: SelectOption[] = [{ value: 'Unknown', label: 'Other account unknown — link later' }, { value: 'External', label: 'Account outside Coiny' }];
  readonly categoryTypes: SelectOption[] = [{ value: 'Expense', label: 'Expense' }, { value: 'Income', label: 'Income' }];
  get classificationOptions(): SelectOption[] { return [{ value: '', label: 'All bank classifications' }, ...[...new Set(this.rows().map(r => r.classification).filter(Boolean))].sort().map(value => ({ value, label: value }))]; }
  get filtered(): ReviewRow[] {
    const query = this.search.trim().toLowerCase();
    return this.rows().filter(r => (!this.classification || r.classification === this.classification) &&
      (!query || `${r.description} ${r.classification} ${r.merchant ?? ''}`.toLowerCase().includes(query)));
  }
  get pageCount(): number { return Math.max(1, Math.ceil(this.filtered.length / 20)); }
  get pageRows(): ReviewRow[] { return this.filtered.slice((Math.min(this.page, this.pageCount) - 1) * 20, Math.min(this.page, this.pageCount) * 20); }
  get included(): ReviewRow[] { return this.rows().filter(r => !r.excluded); }
  get transferCount(): number { return this.included.filter(r => r.categoryChoice === 'transfer').length; }
  get selectedCount(): number { return this.rows().filter(r => r.selected).length; }
  get allFilteredSelected(): boolean { return this.filtered.length > 0 && this.filtered.every(r => r.selected); }
  get bulkOptions(): SelectOption[] { return [{ value: null, label: 'Choose category or Transfer' }, { value: 'transfer', label: 'Transfer — not income/spending' },
    ...this.categories().filter(c => c.categoryType !== 'Transfer').map(c => ({ value: `c:${c.id}`, label: c.name, detail: c.categoryType }))]; }
  get isDebt(): boolean { return ['CreditCard', 'Loan'].includes(this.account.accountType); }
  options(row: ReviewRow): SelectOption[] { return this.bulkOptions.filter(option => !option.detail || option.detail === (row.direction === 'In' ? 'Income' : 'Expense')); }
  check(row: ReviewRow): ImportRowCheck | undefined { return this.lastChecks().find(check => check.rowId === row.line); }
  serverErrors(row: ReviewRow): string[] { return this.validation()?.rows.find(check => check.rowId === row.line)?.errors ?? []; }
  duplicate(row: ReviewRow): boolean { const check = this.check(row); return !!check && (check.duplicateRowIds.length > 0 || check.duplicateTransactionIds.length > 0); }
  ngOnChanges(): void {
    this.rows.set(this.mappedRows.map(row => ({ ...row, errors: [...row.errors], selected: false, categoryChoice: null, counterparty: 'Unknown', allowDuplicate: false })));
    this.balanceAcknowledged = false; this.validation.set(null); this.result.set(null); this.submissionId = crypto.randomUUID(); this.load();
  }
  load(): void {
    this.loading.set(true); this.loadError.set(false);
    forkJoin({ categories: this.categoriesApi.getCategories(), history: this.api.history(this.account.id) })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: data => {
          this.categories.set(data.categories.filter(c => c.isActive)); this.history.set(data.history);
          this.rows.update(rows => rows.map(row => {
            const match = this.categories().find(c => c.name.trim().toLowerCase() === row.classification.toLowerCase() &&
              (c.categoryType === 'Transfer' || c.categoryType === (row.direction === 'In' ? 'Income' : 'Expense')));
            return { ...row, categoryChoice: match ? match.categoryType === 'Transfer' ? 'transfer' : `c:${match.id}` : null };
          }));
          this.loading.set(false);
        },
        error: () => { this.loadError.set(true); this.loading.set(false); },
      });
  }
  changed(keepDuplicates = false): void { this.reviewInvalidated.emit(); this.balanceAcknowledged = false; this.validation.set(null); if (!keepDuplicates) this.lastChecks.set([]); this.finalConfirmed = false; this.error.set(''); this.notice.set(''); this.submissionId = crypto.randomUUID(); }
  directionChanged(row: ReviewRow): void { if (row.categoryChoice !== 'transfer') row.categoryChoice = null; this.changed(); }
  filterChanged(): void { this.page = 1; this.rows().forEach(row => row.selected = false); }
  selectFiltered(selected: boolean): void { const filtered = new Set(this.filtered); this.rows().forEach(row => { if (filtered.has(row)) row.selected = selected; }); }
  excludeSelected(excluded: boolean): void { this.rows().forEach(row => { if (row.selected) row.excluded = excluded; }); this.changed(true); }
  assignSelected(): void {
    if (!this.bulkChoice) return;
    let assigned = 0, incompatible = 0;
    for (const row of this.rows()) if (row.selected && !row.excluded) {
      if (this.options(row).some(option => option.value === this.bulkChoice)) { row.categoryChoice = this.bulkChoice; assigned++; }
      else incompatible++;
    }
    this.changed(true); this.notice.set(`Assigned ${assigned} rows.${incompatible ? ` ${incompatible} rows have the opposite money direction and were left unchanged.` : ''}`);
  }
  localErrors(row: ReviewRow): string[] {
    if (row.excluded) return [];
    const errors: string[] = [];
    if (!row.date || row.date < this.account.openingBalanceDate) errors.push('Choose a date on or after the account opening date.');
    if (!row.description.trim() || row.description.length > 250) errors.push('Enter a description up to 250 characters.');
    if (row.amount === null || !Number.isFinite(row.amount) || row.amount <= 0 || Math.abs(row.amount * 100 - Math.round(row.amount * 100)) > .000001) errors.push('Enter a positive amount with up to two decimals.');
    if (!row.direction) errors.push('Choose money in or out.');
    if (row.merchant && row.merchant.length > 150) errors.push('Shorten the merchant to 150 characters.');
    return errors;
  }
  validate(): void {
    if (this.busy() || this.locked()) return;
    this.error.set(''); this.finalConfirmed = false; this.balanceAcknowledged = false;
    if (!this.included.length) { this.error.set('Include at least one transaction.'); return; }
    if (this.included.some(row => this.localErrors(row).length)) { this.error.set('Correct the dates, descriptions, and amounts, or exclude the flagged rows.'); return; }
    this.setBusy(true);
    this.api.validate(this.request()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: validation => { this.validation.set(validation); this.lastChecks.set(validation.rows); this.setBusy(false); },
      error: response => { this.error.set(response.error?.detail || 'The batch couldn’t be checked. Check the rows and try again.'); this.setBusy(false); },
    });
  }
  import(): void {
    if (this.busy() || this.result()) return;
    if (!this.pendingRequest && (!this.canImportReviewed || !this.finalConfirmed)) return;
    if (this.sessionMode) { this.reviewed.emit(this.request()); return; }
    this.pendingRequest ??= this.request();
    this.locked.set(true); this.lockedChange.emit(true); this.setBusy(true); this.error.set('');
    this.api.import(this.pendingRequest).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: result => {
        this.toasts.success(`${result.importedCount} transactions imported.`); this.result.set(result); this.history.update(items => [result, ...items.filter(item => item.batchId !== result.batchId)]);
        this.setBusy(false); this.completed.emit();
      },
      error: response => {
        this.setBusy(false);
        if (response.status === 422 && response.error?.rows) {
          this.balanceAcknowledged = false; this.validation.set(response.error); this.lastChecks.set(response.error.rows); this.pendingRequest = null; this.locked.set(false); this.lockedChange.emit(false); this.finalConfirmed = false;
          this.error.set('The ledger changed or some rows need attention. Review the updated checks before importing.');
        } else if ([400, 403, 409].includes(response.status)) {
          this.pendingRequest = null; this.locked.set(false); this.lockedChange.emit(false); this.validation.set(null); this.finalConfirmed = false;
          this.error.set(response.error?.detail || 'The batch was rejected. Check its details before trying again.');
        } else this.error.set('We couldn’t confirm the result. Retry this same batch to safely check or finish it; your rows are locked to prevent duplicate imports.');
      },
    });
  }
  saveCategory(): void {
    if (this.busy() || this.locked()) return;
    const name = this.categoryName.trim();
    if (!name || name.length > 100) { this.error.set('Enter a category name of up to 100 characters.'); return; }
    this.setBusy(true); this.error.set('');
    this.categoriesApi.createCategory({ name, categoryType: this.categoryType }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: category => {
        this.categories.update(items => [...items, category].sort((a, b) => a.name.localeCompare(b.name)));
        this.bulkChoice = `c:${category.id}`; this.categoryName = ''; this.creatingCategory.set(false); this.setBusy(false);
        this.notice.set('Category added. Select rows and apply it using the bulk category control.');
      },
      error: response => { this.error.set(typeof response.error === 'string' ? response.error : 'The category couldn’t be saved.'); this.setBusy(false); },
    });
  }
  private request(): TransactionImportRequest {
    return { ...(this.bankBalance ? { bankBalanceCheck: { ...this.bankBalance, acknowledgedCalculatedBalance: this.balanceAcknowledged ? this.validation()?.balanceComparison?.calculatedBalance ?? null : null } } : {}), accountId: this.account.id, filename: this.filename || 'transactions.csv', submissionId: this.submissionId,
      rows: this.included.map(row => ({ rowId: row.line, date: row.date || null, amount: row.amount, direction: row.direction,
        categoryType: row.categoryChoice === 'transfer' ? 'Transfer' : row.direction === 'In' ? 'Income' : 'Expense',
        categoryId: row.categoryChoice?.startsWith('c:') ? Number(row.categoryChoice.slice(2)) : null,
        counterparty: row.counterparty, description: row.description.trim(), merchant: row.merchant?.trim() || null,
        notes: null, isCleared: this.cleared, allowDuplicate: row.allowDuplicate,
      })) };
  }
  private setBusy(value: boolean): void { this.busy.set(value); this.busyChange.emit(value); }
}
