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
  async function render(rows = [mapped()]) {
    const f = TestBed.createComponent(ImportReview);
    f.componentRef.setInput('mappedRows', rows);
    f.componentRef.setInput('account', { id: 1, name: 'Checking', accountType: 'Checking', openingBalanceDate: '2026-10-01' });
    f.componentRef.setInput('filename', 'bank.csv'); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/categories')).flush([
      { id: 1, name: 'Dining', categoryType: 'Expense', isActive: true },
      { id: 2, name: 'Salary', categoryType: 'Income', isActive: true },
    ]);
    http.expectOne(r => r.url.endsWith('/transaction-imports')).flush([]);
    f.detectChanges(); await f.whenStable(); return f;
  }
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
});
