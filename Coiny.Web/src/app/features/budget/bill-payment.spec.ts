import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BillPayment } from './bill-payment';
const bill = { sourceKey: 'a0000000-0000-0000-0000-000000000001', date: '2026-10-17', itemId: 1, name: 'Electric', isIncome: false, allocationType: 'Bills' as const, amount: 100 };
describe('BillPayment', () => {
  let http: HttpTestingController;
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value() {} });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value() {} });
  });
  afterAll(() => { Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype, 'close'); });
  beforeEach(() => { TestBed.configureTestingModule({ imports: [BillPayment], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function render(payment?: { isPaid: boolean; paidOn: string | null; note: string | null; transactionId: number | null }) {
    const f = TestBed.createComponent(BillPayment); f.componentRef.setInput('bill', { ...bill, payment }); f.detectChanges(); return f;
  }
  it('saves a manual payment without creating a ledger transaction', () => {
    const f = render(); const c = f.componentInstance; const saved = vi.fn(); c.saved.subscribe(saved);
    c.paidOn = '2026-10-15'; c.note = 'Paid online'; c.save();
    const request = http.expectOne(r => r.url.endsWith('/2026-10-17/payment'));
    expect(request.request.method).toBe('PUT'); expect(request.request.body).toEqual({ isPaid: true, paidOn: '2026-10-15', note: 'Paid online', transactionId: null });
    request.flush(request.request.body); expect(saved).toHaveBeenCalledOnce();
  });
  it('linking marks paid by default, but unlinking keeps its date and status', () => {
    const f = render(); const c = f.componentInstance; c.isPaid = false; c.find();
    const entry = { id: 7, date: '2026-10-15', amount: 100, description: 'Electric', accountName: 'Checking', categoryName: 'Utilities', exactAmount: true };
    http.expectOne(r => r.url.endsWith('/transactions')).flush([entry]);
    c.choose(entry); expect(c.isPaid).toBe(true); expect(c.paidOn).toBe('2026-10-15'); expect(c.transactionId).toBe(7);
    c.transactionId = null; expect(c.isPaid).toBe(true); c.save();
    const request = http.expectOne(r => r.url.endsWith('/payment')); expect(request.request.body.transactionId).toBeNull(); request.flush(request.request.body);
  });
  it('marking unpaid detaches the linked transaction and retains errors for correction', () => {
    const f = render({ isPaid: true, paidOn: '2026-10-15', note: null, transactionId: 7 }); const c = f.componentInstance;
    c.unpaid(); c.save(); const request = http.expectOne(r => r.url.endsWith('/payment'));
    expect(request.request.body).toMatchObject({ isPaid: false, paidOn: null, transactionId: null });
    request.flush({ detail: 'Please reload this bill.' }, { status: 409, statusText: 'Conflict' }); f.detectChanges();
    expect(c.busy()).toBe(false); expect(f.nativeElement.querySelector('[role="alert"]').textContent).toContain('Please reload');
  });
});
