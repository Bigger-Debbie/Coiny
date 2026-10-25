import { MerchantRule } from '../../../core/services/merchant-rules.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ImportReview } from './import-review';
import { ImportReviewRow } from './csv-mapping';
import { TransactionImportValidation } from '../../../shared/models/transactions/transaction-import';

describe('Import transaction review', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ImportReview], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  const mapped = (overrides: Partial<ImportReviewRow> = {}): ImportReviewRow => ({
    line: 2, date: '2026-10-16', description: 'Coffee', amount: 5, direction: 'Out', classification: 'Dining',
    merchant: null, sourceAccount: null, status: 'Posted', excluded: false, exclusionReason: null, errors: [], ...overrides,
  });
  async function render(rows = [mapped()], bank: { date: string; balance: number } | null = null, rules:MerchantRule[] = []) {
    const f = TestBed.createComponent(ImportReview);
    f.componentRef.setInput('mappedRows', rows); f.componentRef.setInput('bankBalance', bank);
    f.componentRef.setInput('account', { id: 1, name: 'Checking', accountType: 'Checking', openingBalanceDate: '2026-10-01' });
    f.componentRef.setInput('filename', 'bank.csv'); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/categories')).flush([
      { id: 1, name: 'Dining', categoryType: 'Expense', isActive: true },
      { id: 2, name: 'Salary', categoryType: 'Income', isActive: true },
    ]);
    http.expectOne(r => r.url.endsWith('/transaction-imports')).flush([]);
    http.expectOne(r=>r.url.endsWith('/merchant-rules')).flush(rules);
    f.detectChanges(); await f.whenStable(); return f;
  }
  it('suggests categories before bank fallback, preserves overrides on refresh, and exposes remembering the merchant',async()=>{
    const rule:MerchantRule={id:1,matchType:'MerchantExact',pattern:'Coffee shop',categoryId:1,categoryName:'Dining',categoryType:'Expense',categoryActive:true,isActive:true,editVersion:1};
    const f=await render([mapped({merchant:'Coffee shop',classification:''})],null,[rule]);const c=f.componentInstance;
    expect(c.rows()[0].categoryChoice).toBe('c:1');expect(c.rows()[0].ruleSource).toBe('Coffee shop');expect(f.nativeElement.textContent).toContain('Suggested by rule');
    c.detailLine.set(2);f.detectChanges();expect(f.nativeElement.textContent).toContain('Remember this merchant');
    c.edit(c.rows()[0],'categoryChoice',null);c.refreshRules();http.expectOne(r=>r.url.endsWith('/merchant-rules')).flush([rule]);
    expect(c.rows()[0].categoryChoice).toBeNull();expect(c.rows()[0].ruleSource).toBeUndefined();
    c.edit(c.rows()[0],'categoryChoice','c:1');c.rememberMerchant(c.rows()[0]);expect(c.remember()).toEqual({pattern:'Coffee shop',categoryId:1});
  });
  it('leaves conflicting rules for review and rematches rule-owned categories after merchant edits',async()=>{
    const rule:MerchantRule={id:1,matchType:'MerchantExact',pattern:'Coffee shop',categoryId:1,categoryName:'Dining',categoryType:'Expense',categoryActive:true,isActive:true,editVersion:1};
    const f=await render([mapped({merchant:'Coffee shop',classification:''})],null,[rule]);const c=f.componentInstance;
    c.edit(c.rows()[0],'merchant','Other shop');expect(c.rows()[0].categoryChoice).toBeNull();
    c.rules.set([{...rule,matchType:'DescriptionContains',pattern:'Coffee'}, {...rule,id:2,matchType:'DescriptionContains',pattern:'Coffee',categoryId:3}]);
    c.refreshRules();http.expectOne(r=>r.url.endsWith('/merchant-rules')).flush(c.rules());
    expect(c.statusLabel(c.rows()[0])).toBe('Conflicting rules');c.edit(c.rows()[0],'categoryChoice','c:1');expect(c.rows()[0].ruleConflict).toBe(false);
  });
  it('shows the continue action only after successful checks and hides it after edits', async () => {
    const f=await render();const c=f.componentInstance;
    expect(f.nativeElement.querySelector('.review-next-actions')).toBeNull();
    c.validate();http.expectOne(r=>r.url.endsWith('/validate')).flush(valid());f.detectChanges();
    expect(f.nativeElement.querySelector('.review-next-actions button')).toBeTruthy();
    c.changed(true);f.detectChanges();expect(f.nativeElement.querySelector('.review-next-actions')).toBeNull();
  });
  it('requires balance difference acknowledgment and binds it to the reviewed calculation', async () => {
    const f = await render([mapped()], { date: '2026-10-16', balance: 1000 }); const c = f.componentInstance;
    c.validate(); const preview = http.expectOne(r => r.url.endsWith('/validate'));
    expect(preview.request.body.bankBalanceCheck).toEqual({ date: '2026-10-16', balance: 1000, acknowledgedCalculatedBalance: null });
    preview.flush({ ...valid(), canImport: false, balanceComparison: { date: '2026-10-16', bankBalance: 1000, calculatedBalance: 995, difference: 5, requiresAcknowledgment: true } });
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('Bank balance differs');
    c.finalConfirmed = true; c.import(); http.expectNone(r => r.method === 'POST' && !r.url.endsWith('/validate'));
    c.balanceAcknowledged = true; c.finalConfirmed = true; c.import();
    const save = http.expectOne(r => r.method === 'POST' && !r.url.endsWith('/validate'));
    expect(save.request.body.bankBalanceCheck.acknowledgedCalculatedBalance).toBe(995);
    save.flush({ ...valid(), canImport: false, balanceComparison: { date: '2026-10-16', bankBalance: 1000, calculatedBalance: 994, difference: 6, requiresAcknowledgment: true } }, { status: 422, statusText: 'Review again' });
    expect(c.balanceAcknowledged).toBe(false); expect(c.canImportReviewed).toBe(false); expect(c.locked()).toBe(false);
  });
  it('retains the original bank reference when an overlapping source row is skipped', async () => {
    const f = await render([mapped(), mapped({ line: 3, date: '2026-10-15' })], { date: '2026-10-16', balance: 1000 }); const c = f.componentInstance;
    c.rows()[0].excluded = true; c.changed(); c.validate();
    const request = http.expectOne(r => r.url.endsWith('/validate'));
    expect(request.request.body.rows.map((r: { rowId: number }) => r.rowId)).toEqual([3]);
    expect(request.request.body.bankBalanceCheck).toMatchObject({ date: '2026-10-16', balance: 1000 });
    request.flush(valid([3]));
  });
  function valid(rowIds = [2]): TransactionImportValidation {
    return { rows: rowIds.map(rowId => ({ rowId, errors: [], duplicateTransactionIds: [], duplicateRowIds: [] })), canImport: true, currentBalance: 1000, projectedBalance: 995 };
  }
  function validate(c: ImportReview, response = valid()) {
    c.validate(); const request = http.expectOne(r => r.url.endsWith('/transaction-imports/validate'));
    request.flush(response); return request.request.body;
  }
  const result = { batchId: 7, submissionId: 'example', accountId: 1, filename: 'bank.csv', importedCount: 1, completedAt: '2026-10-17T14:00:00Z', balanceAfter: 995 };

  it('suggests exact categories and imports only included rows after checking and confirmation', async () => {
    const f = await render([mapped(), mapped({ line: 3, excluded: true, status: 'Pending' })]); const c = f.componentInstance;
    expect(c.rows()[0].categoryChoice).toBe('c:1');
    c.import(); http.expectNone(r => r.method === 'POST');
    const body = validate(c);
    expect(body.rows).toHaveLength(1); expect(body.rows[0]).toMatchObject({ rowId: 2, categoryId: 1, categoryType: 'Expense', amount: 5 });
    c.import(); http.expectNone(r => r.url.endsWith('/transaction-imports') && r.method === 'POST');
    c.finalConfirmed = true; c.import(); c.import();
    http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transaction-imports')).flush(result);
    f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.textContent).toContain('Import complete'); expect(c.history()).toHaveLength(1);
  });
  it('allows bulk assignment while protecting direction and invalidating prior checks', async () => {
    const f = await render([mapped(), mapped({ line: 3, direction: 'In', classification: '' })]); const c = f.componentInstance;
    c.selectFiltered(true); c.bulkChoice = 'c:1'; c.assignSelected();
    expect(c.rows()[0].categoryChoice).toBe('c:1'); expect(c.rows()[1].categoryChoice).toBeNull();
    expect(c.notice()).toContain('opposite money direction');
    c.bulkChoice = 'transfer'; c.assignSelected();
    const body = validate(c, valid([2, 3])); expect(body.rows.every((row: {categoryType: string}) => row.categoryType === 'Transfer')).toBe(true);
    expect(body.rows[0].counterparty).toBe('Unknown');
    c.excludeSelected(true); expect(c.included).toHaveLength(0); expect(c.validation()).toBeNull();
  });
  it('keeps duplicate decisions visible across edits and sends explicit overrides', async () => {
    const f = await render([mapped(), mapped({ line: 3 })]); const c = f.componentInstance;
    validate(c, { ...valid([2, 3]), canImport: false, rows: [
      { rowId: 2, errors: ['Possible duplicate'], duplicateTransactionIds: [10], duplicateRowIds: [3] },
      { rowId: 3, errors: ['Possible duplicate'], duplicateTransactionIds: [], duplicateRowIds: [2] },
    ] });
    c.rows()[0].allowDuplicate = true; c.changed(true);
    expect(c.duplicate(c.rows()[1])).toBe(true);
    c.rows()[1].excluded = true; c.changed(true);
    const body = validate(c); expect(body.rows).toHaveLength(1); expect(body.rows[0].allowDuplicate).toBe(true);
    c.edit(c.rows()[0], 'amount', 6); expect(c.rows()[0].allowDuplicate).toBe(false);
  });
  it('retries an uncertain save with the identical submission and payload', async () => {
    const f = await render(); const c = f.componentInstance;
    validate(c); c.finalConfirmed = true; c.import();
    const first = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transaction-imports'));
    const payload = first.request.body;
    first.error(new ProgressEvent('error'));
    expect(c.locked()).toBe(true); expect(c.error()).toContain('Retry');
    c.import();
    const retry = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transaction-imports'));
    expect(retry.request.body).toEqual(payload); retry.flush(result);
    expect(c.result()?.batchId).toBe(7);
  });
  it('shows authoritative commit-time duplicate changes without losing the review', async () => {
    const f = await render(); const c = f.componentInstance;
    validate(c); c.finalConfirmed = true; c.import();
    const response = { ...valid(), canImport: false, rows: [{ rowId: 2, errors: ['Possible duplicate'], duplicateTransactionIds: [99], duplicateRowIds: [] }] };
    http.expectOne(r => r.method === 'POST' && r.url.endsWith('/transaction-imports')).flush(response, { status: 422, statusText: 'Unprocessable Entity' });
    expect(c.locked()).toBe(false); expect(c.duplicate(c.rows()[0])).toBe(true);
    expect(c.rows()[0].description).toBe('Coffee'); expect(c.finalConfirmed).toBe(false);
  });
  it('creates a household category during review and makes it available for assignment', async () => {
    const f = await render(); const c = f.componentInstance;
    c.categoryName = ' Snacks '; c.saveCategory();
    const request = http.expectOne(r => r.url.endsWith('/categories') && r.method === 'POST');
    expect(request.request.body).toEqual({ name: 'Snacks', categoryType: 'Expense' });
    request.flush({ id: 3, name: 'Snacks', categoryType: 'Expense', isActive: true });
    c.selectFiltered(true); c.assignSelected(); expect(c.rows()[0].categoryChoice).toBe('c:3');
  });
  it('uses compact row statuses and opens explanations on demand', async () => {
    const f = await render([mapped(), mapped({ line: 3, classification: '' }), mapped({ line: 4, date: null }), mapped({ line: 5, excluded: true })]);
    const c = f.componentInstance;
    expect(c.rows().map(row => c.status(row))).toEqual(['ready', 'review', 'error', 'excluded']);
    expect(f.nativeElement.querySelector('.sheet-scroll')).toBeTruthy();
    expect(f.nativeElement.querySelector('.row-ready .status-pill').textContent).toContain('Ready to check');
    expect(f.nativeElement.querySelector('#row-details')).toBeNull();
    f.nativeElement.querySelector('.row-error .status-pill').click(); f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('#row-details').textContent).toContain('opening date');
    expect(f.nativeElement.querySelectorAll('app-review-cell input')).toHaveLength(0);
  });
  it('resizes and resets column widths without changing review data or validation', () => {
    const f=TestBed.createComponent(ImportReview); const c=f.componentInstance;
    const original=c.rows(); const validation=c.validation();
    c.resizeColumn(3,500); expect(c.columnWidths()[3]).toBe(500);
    c.resizeColumn(3,1); expect(c.columnWidths()[3]).toBe(c.columns[3].min);
    c.resizeColumn(3,9999); expect(c.columnWidths()[3]).toBe(c.columns[3].max);
    c.resetColumnWidths(); expect(c.columnWidths()).toEqual(c.columns.map(column=>column.width));
    expect(c.rows()).toBe(original); expect(c.validation()).toBe(validation);
  });

});
