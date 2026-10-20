import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NgForm } from '@angular/forms';
import { EditTransaction } from './edit-transaction';
import { TransactionEditDetails } from '../../../shared/models/transactions/transaction-management';
const details: TransactionEditDetails = { transaction: { id: 7, editVersion: 2, accountId: 1, categoryId: 1, accountName: 'Checking', accountType: 'Checking', institutionName: 'Bank', categoryName: 'Food', categoryType: 'Expense', amount: 25, transactionDate: '2026-10-16T00:00:00', description: 'Dinner', merchant: 'Cafe', notes: 'Original note', isCleared: true, importBatchId: 5 }, pairedEditVersion: null, bill: { allocationKey: 'bill', dueDate: '2026-10-16', isPaid: true } };
describe('EditTransaction', () => {
  let http: HttpTestingController;
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value() {} });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value() {} });
  });
  afterAll(() => { Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype, 'close'); });
  beforeEach(() => { TestBed.configureTestingModule({ imports: [EditTransaction], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  async function render(value = details, editing = true) {
    const f = TestBed.createComponent(EditTransaction); f.componentRef.setInput('transactionId', 7); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/transactions/7')).flush(value);
    http.expectOne(r => r.url.endsWith('/accounts')).flush([{ id: 1, name: 'Checking', isActive: true, openingBalanceDate: '2026-10-01' }]);
    http.expectOne(r => r.url.endsWith('/categories')).flush([{ id: 1, name: 'Food', categoryType: 'Expense', isActive: true }]);
    f.detectChanges(); await f.whenStable();
    if (editing) { f.componentInstance.startEditing(); f.detectChanges(); await f.whenStable(); }
    return f;
  }
  it('opens read-only details and switches the same modal into editing', async () => {
    const f = await render(details, false); const c = f.componentInstance;
    expect(f.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Transaction Details');
    expect(f.nativeElement.querySelector('form')).toBeNull();
    for (const text of ['Dinner', 'Checking', 'Bank', 'Food', 'Cafe', 'Original note', 'Cleared', 'Batch #5', 'Paid']) expect(f.nativeElement.textContent).toContain(text);
    f.nativeElement.querySelector('[data-edit-transaction]').click(); f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Edit Transaction');
    expect(f.nativeElement.querySelector('#edit-notes').value).toBe('Original note');
    expect(f.nativeElement.querySelectorAll('dialog')).toHaveLength(1);
    c.request!.notes = 'Discard this'; vi.spyOn(window, 'confirm').mockReturnValue(true); c.cancelEditing(); f.detectChanges(); await f.whenStable();
    expect(c.editing()).toBe(false); expect(c.dirty).toBe(false); expect(c.request!.notes).toBe('Original note');
    expect(f.nativeElement.querySelector('form')).toBeNull();
  });
  it('loads notes and provenance, saves corrections with a version, and emits refresh', async () => {
    const f = await render(); const c = f.componentInstance; const changed = vi.fn(); c.changed.subscribe(changed);
    expect(c.request?.notes).toBe('Original note'); expect(c.dirty).toBe(false); expect(f.nativeElement.textContent).toContain('Imported transaction');
    c.request!.amount = 30; c.save({ invalid: false } as NgForm);
    const request = http.expectOne(r => r.method === 'PUT'); expect(request.request.body).toMatchObject({ amount: 30, expectedEditVersion: 2, notes: 'Original note' });
    request.flush(details); expect(changed).toHaveBeenCalledWith('updated');
  });
  it('requires explicit deletion confirmation and preserves the bill status explanation', async () => {
    const f = await render(); const c = f.componentInstance;
    expect(f.nativeElement.textContent).toContain('Delete transaction'); http.expectNone(r => r.method === 'DELETE');
    const button = [...f.nativeElement.querySelectorAll('button')].find((b: unknown) => (b as HTMLButtonElement).textContent?.trim() === 'Delete transaction') as HTMLButtonElement;
    button.click(); f.detectChanges(); expect(f.nativeElement.textContent).toContain('paid status will stay');
    c.remove(); const request = http.expectOne(r => r.method === 'DELETE'); expect(request.request.params.get('expectedEditVersion')).toBe('2'); request.flush(null);
  });
  it('explains linked transfer scope and sends both versions when deleting', async () => {
    const f = await render({ ...details, transaction: { ...details.transaction, categoryType: 'Transfer', pairedTransactionId: 8, counterpartyAccountName: 'Savings', counterpartyAccountId: 2, transferDirection: 'Out', transferCounterparty: 'Linked' }, pairedEditVersion: 4, bill: null });
    const c = f.componentInstance; expect(f.nativeElement.textContent).toContain('Amount changes update both sides');
    expect(c.kinds.map(k => k.value)).toEqual(['Transfer']); c.deleting.set(true); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Delete both entries'); c.remove();
    const request = http.expectOne(r => r.method === 'DELETE'); expect(request.request.params.get('expectedPairedEditVersion')).toBe('4'); request.flush(null);
  });
  it('blocks stale saves until reloaded and warns before discarding edits', async () => {
    const f = await render(); const c = f.componentInstance; c.request!.notes = 'Unsaved';
    c.save({ invalid: false } as NgForm); http.expectOne(r => r.method === 'PUT').flush({ detail: 'Transaction changed.' }, { status: 409, statusText: 'Conflict' });
    expect(c.stale()).toBe(true); c.save({ invalid: false } as NgForm); http.expectNone(r => r.method === 'PUT');
    vi.spyOn(window, 'confirm').mockReturnValue(false); const dismiss = vi.fn(); c.dismiss.subscribe(dismiss); c.close(); expect(dismiss).not.toHaveBeenCalled();
    expect(c.request!.notes).toBe('Unsaved');
  });
});
