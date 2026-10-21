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
  it('offers transfer linking for a contribution and marks it paid without creating a transaction', () => {
    const f = TestBed.createComponent(BillPayment); f.componentRef.setInput('bill', { ...bill, allocationType: 'Investments', name: 'IRA', payment: { isPaid: false, paidOn: null, note: null, transactionId: null } }); f.detectChanges();
    const c = f.componentInstance;
    f.nativeElement.querySelector('.link-section button').click(); f.detectChanges();
    const entry = { id: 12, date: '2026-10-15', amount: 100, description: 'Contribution', accountName: 'IRA', categoryName: 'Transfer', categoryType: 'Transfer' as const, transferDirection: 'In' as const, exactAmount: true };
    http.expectOne(r => r.url.endsWith('/transactions')).flush([entry]); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Mark this contribution paid'); expect(f.nativeElement.textContent).toContain('expense or transfer'); expect(f.nativeElement.textContent).toContain('Money in');
    f.nativeElement.querySelector('.candidate').click(); expect(c.isPaid).toBe(true); expect(c.paidOn).toBe(entry.date);
    c.save(); const request = http.expectOne(r => r.url.endsWith('/payment')); expect(request.request.body).toMatchObject({ isPaid: true, transactionId: 12 }); request.flush(request.request.body);
    http.expectNone(r => r.method === 'POST');
  });
  it('opens the picker directly for awaiting payments and keeps the original payment date when linking', () => {
    const f = TestBed.createComponent(BillPayment);
    f.componentRef.setInput('bill', { ...bill, payment: { isPaid: true, paidOn: '2026-10-14', note: null, transactionId: null } });
    f.componentRef.setInput('startLinking', true); f.detectChanges();
    const entry = { id: 9, date: '2026-10-15', amount: 105, description: 'Electric', accountName: 'Bills', categoryName: 'Utilities', exactAmount: false };
    http.expectOne(r => r.url.endsWith('/transactions')).flush([entry]); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Paid · Awaiting transaction');
    f.nativeElement.querySelector('.candidate').click(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Paid · Linked'); expect(f.componentInstance.paidOn).toBe('2026-10-14');
    const unlink = [...f.nativeElement.querySelectorAll('button')].find((b: any) => b.textContent.includes('Unlink transaction')) as HTMLButtonElement;
    unlink.click(); f.detectChanges(); expect(f.nativeElement.textContent).toContain('Paid · Awaiting transaction');
    expect(f.componentInstance.isPaid).toBe(true);
  });
});
