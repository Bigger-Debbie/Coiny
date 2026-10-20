import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TransferReconciliation } from './transfer-reconciliation';
import { Transaction } from '../../../shared/models/transactions/transaction';

const source: Transaction = { id: 1, editVersion: 2, accountId: 1, accountName: 'Bills', institutionName: 'Bank', categoryId: 5, categoryName: 'Transfer', categoryType: 'Transfer', amount: 250, transactionDate: '2026-10-16T00:00:00', description: 'Transfer to Spending', merchant: null, isCleared: true, transferDirection: 'Out', transferCounterparty: 'Unknown', importBatchId: 4 };
const candidate: Transaction = { ...source, id: 2, accountId: 2, accountName: 'Spending', transferDirection: 'In', editVersion: 3, transactionDate: '2026-10-17T00:00:00', description: 'Transfer from Bills', importBatchId: 5 };

describe('TransferReconciliation', async () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  async function render() {
    const f = TestBed.createComponent(TransferReconciliation); f.componentRef.setInput('transaction', source);
    f.componentRef.setInput('accounts', [{ id: 1, name: 'Bills', isActive: true }, { id: 2, name: 'Spending', isActive: true, openingBalanceDate: '2026-10-01' }]); f.detectChanges();
    await f.whenStable(); f.detectChanges(); return { f, c: f.componentInstance };
  }
  function search(c: TransferReconciliation, rows = [candidate]) {
    c.accountId = 2; c.search(); const request = http.expectOne(r => r.url.endsWith('/entries/1/candidates'));
    expect(request.request.params.get('accountId')).toBe('2'); request.flush(rows);
  }
  it('requires a manual choice and links existing entries without invoking creation endpoints', async () => {
    const { f, c } = await render(); search(c); f.changeDetectorRef.markForCheck(); await f.whenStable(); f.detectChanges();
    expect(c.selected()).toBeNull(); expect(f.nativeElement.textContent).toContain('No transaction is added');
    expect(f.nativeElement.textContent).toContain('Different posting date'); expect(f.nativeElement.textContent).toContain('Batch #5');
    c.link(); http.expectNone(r => r.method === 'POST');
    const completed = vi.fn(); c.completed.subscribe(completed); c.selected.set(candidate); c.link();
    const request = http.expectOne(r => r.url.endsWith('/transfers/links'));
    expect(request.request.body).toEqual({ firstTransactionId: 1, secondTransactionId: 2, expectedFirstEditVersion: 2, expectedSecondEditVersion: 3 });
    request.flush({ outgoing: source, incoming: candidate }); expect(completed).toHaveBeenCalled();
    http.expectNone(r => r.url.endsWith('/counterpart') || r.url.endsWith('/transfers'));
  });
  it('makes counterpart creation explicit and requires acknowledgment even when matches exist', async () => {
    const { f, c } = await render(); search(c); c.chooseMode('create'); f.changeDetectorRef.markForCheck(); await f.whenStable(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Possible matches already exist');
    c.create(); http.expectNone(r => r.method === 'POST'); c.confirmed = true;
    c.create(); const request = http.expectOne(r => r.url.endsWith('/entries/1/counterpart'));
    expect(request.request.body).toEqual({ accountId: 2, date: '2026-10-16', expectedEditVersion: 2 }); request.flush({ outgoing: source, incoming: candidate });
  });
  it('discards old matches and confirmations when the date window changes', async () => {
    const { c } = await render(); search(c); c.selected.set(candidate); c.confirmed = true; c.days = 30; c.search();
    expect(c.selected()).toBeNull(); expect(c.confirmed).toBe(false); expect(c.searched()).toBe(false);
    c.link(); c.create(); http.expectNone(r => r.method === 'POST');
    const request = http.expectOne(r => r.url.endsWith('/candidates')); expect(request.request.params.get('days')).toBe('30'); request.flush([]);
  });
  it('prevents stale matches from being retried until the transaction is reloaded', async () => {
    const { c } = await render(); const stale = vi.fn(); c.staleDetected.subscribe(stale); search(c); c.selected.set(candidate); c.link();
    http.expectOne(r => r.url.endsWith('/links')).flush({ detail: 'Entry changed.' }, { status: 409, statusText: 'Conflict' });
    expect(stale).toHaveBeenCalled(); expect(c.stale()).toBe(true); expect(c.busy()).toBe(false); c.link(); c.create(); http.expectNone(r => r.method === 'POST');
  });
  it('shows ambiguous matches without selecting one and allows a failed search to be retried', async () => {
    const { c } = await render(); c.accountId = 2; c.search();
    http.expectOne(r => r.url.endsWith('/candidates')).flush({}, { status: 500, statusText: 'Error' });
    expect(c.searched()).toBe(false); c.confirmed = true; c.create(); http.expectNone(r => r.method === 'POST');
    search(c, [candidate, { ...candidate, id: 3 }]); expect(c.candidates()).toHaveLength(2); expect(c.selected()).toBeNull();
  });
});
