import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReconcileAccount } from './reconcile-account';

const account = { id: 1, name: 'Checking', accountType: 'Checking', institutionId: null, institutionName: null, openingBalance: 1000, openingBalanceDate: '2026-01-01', currentBalance: 1200, isActive: true };
const check = { statementDate: '2026-10-05', statementBalance: 900, calculatedBalance: 900, difference: 0, matches: true, ledgerFingerprint: 'a'.repeat(64), transactionCount: 5 };
describe('ReconcileAccount', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ReconcileAccount], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function render(type = 'Checking') {
    const f = TestBed.createComponent(ReconcileAccount);
    f.componentRef.setInput('account', { ...account, accountType: type });
    f.detectChanges(); http.expectOne(r => r.url.endsWith('/1/reconciliations')).flush([]);
    f.componentInstance.statementDate = check.statementDate; f.componentInstance.statementBalance = check.statementBalance;
    return f;
  }
  it('requires explicit transaction review and submits the checked fingerprint without changing cleared status', () => {
    const f = render(); const c = f.componentInstance; const saved = vi.fn(); c.saved.subscribe(saved);
    c.checkBalance();
    const request = http.expectOne(r => r.url.endsWith('/reconciliations/check'));
    expect(request.request.body).toEqual({ statementDate: check.statementDate, statementBalance: 900 }); request.flush(check);
    c.confirm(); http.expectNone(r => r.method === 'POST');
    c.reviewed = true; c.confirm();
    const confirm = http.expectOne(r => r.url.endsWith('/reconciliations') && r.method === 'POST');
    expect(confirm.request.body).toEqual({ statementDate: check.statementDate, statementBalance: 900, reviewedFingerprint: check.ledgerFingerprint, transactionsReviewed: true });
    confirm.flush({ id: 1 }); expect(saved).toHaveBeenCalledOnce(); expect(c.busy()).toBe(false);
  });
  it('blocks mismatches and clears a successful check when statement inputs change', () => {
    const f = render(); const c = f.componentInstance;
    c.checkBalance(); http.expectOne(r => r.method === 'POST').flush({ ...check, matches: false, calculatedBalance: 850, difference: 50 });
    c.reviewed = true; c.confirm(); http.expectNone(r => r.method === 'POST');
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('Balances don’t match yet'); expect(f.nativeElement.textContent).toContain('$50.00');
    c.check.set(check); c.reviewed = true; c.changed(); expect(c.check()).toBeNull(); expect(c.reviewed).toBe(false);
  });
  it('shows a stale-ledger conflict and requires a fresh check after a failed save', () => {
    const f = render(); const c = f.componentInstance;
    c.check.set(check); c.reviewed = true; c.confirm();
    http.expectOne(r => r.method === 'POST').flush({ detail: 'The account changed after your check.' }, { status: 409, statusText: 'Conflict' });
    expect(c.check()).toBeNull(); expect(c.reviewed).toBe(false); expect(c.error()).toContain('account changed');
    c.confirm(); http.expectNone(r => r.method === 'POST');
  });
  it('shows verified and invalidated history with reviewer and retains retry for load failure', () => {
    const f = render(); const c = f.componentInstance;
    c.loadHistory(); http.expectOne(r => r.method === 'GET').flush('failed', { status: 500, statusText: 'Error' });
    expect(c.historyError()).toBe(true);
    c.loadHistory(); http.expectOne(r => r.method === 'GET').flush([
      { id: 2, statementDate: '2026-10-05', statementBalance: 900, confirmedAt: '2026-10-06T12:00:00Z', confirmedByName: 'Alex', needsReview: false },
      { id: 1, statementDate: '2026-10-04', statementBalance: 1000, confirmedAt: '2026-10-05T12:00:00Z', confirmedByName: 'Hannah', needsReview: true, invalidationReason: 'Transactions in this verified period changed.' },
    ]);
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('✓ Verified'); expect(f.nativeElement.textContent).toContain('Needs review'); expect(f.nativeElement.textContent).toContain('Confirmed by Hannah');
  });
  it('labels debt correctly and emits the checked period for statement review', () => {
    const f = render('Loan'); const c = f.componentInstance; c.check.set(check); const inspect = vi.fn(); c.inspect.subscribe(inspect);
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('Statement amount owed');
    const button = Array.from(f.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(b => b.textContent?.includes('Review these'))!;
    button.click(); expect(inspect).toHaveBeenCalledWith({ date: check.statementDate, balance: 900 });
  });
  it('restores statement inputs after reviewing transactions but requires a new check', () => {
    const f = TestBed.createComponent(ReconcileAccount);
    f.componentRef.setInput('account', account); f.componentRef.setInput('initialStatement', { date: '2026-09-30', balance: 850 });
    f.detectChanges(); http.expectOne(r => r.method === 'GET').flush([]);
    expect(f.componentInstance.statementDate).toBe('2026-09-30'); expect(f.componentInstance.statementBalance).toBe(850); expect(f.componentInstance.check()).toBeNull();
  });
  it('requires both confirmations and a reason before creating an adjustment',()=>{
    const f=render();const c=f.componentInstance;const saved=vi.fn();c.saved.subscribe(saved);
    c.check.set({...check,matches:false,statementBalance:950,difference:50});c.adjustmentOpen=true;
    c.adjust();http.expectNone(r=>r.method==='POST');
    c.reviewed=true;c.adjustmentReason='Reviewed missing history';c.adjust();http.expectNone(r=>r.method==='POST');
    c.adjustmentAcknowledged=true;c.adjust();
    const request=http.expectOne(r=>r.url.endsWith('/reconciliations/adjust'));
    expect(request.request.body).toMatchObject({statementDate:check.statementDate,statementBalance:950,reviewedFingerprint:check.ledgerFingerprint,transactionsReviewed:true,adjustmentAcknowledged:true,reason:'Reviewed missing history'});
    expect(request.request.body.operationId).toMatch(/^[a-f0-9-]{36}$/i);
    request.flush({id:5,adjustmentTransactionId:10});expect(saved).toHaveBeenCalledOnce();
  });
  it('retries a lost response using the same adjustment operation and requires a new check on conflict',()=>{
    const f=render();const c=f.componentInstance;c.check.set({...check,matches:false,difference:50});
    c.reviewed=true;c.adjustmentAcknowledged=true;c.adjustmentReason='Statement reviewed';c.adjust();
    const first=http.expectOne(r=>r.url.endsWith('/adjust'));const operation=first.request.body.operationId;
    first.flush('network failure',{status:503,statusText:'Unavailable'});
    c.adjust();const retry=http.expectOne(r=>r.url.endsWith('/adjust'));expect(retry.request.body.operationId).toBe(operation);
    retry.flush({detail:'Account changed'},{status:409,statusText:'Conflict'});
    expect(c.check()).toBeNull();expect(c.adjustmentAcknowledged).toBe(false);expect(c.reviewed).toBe(false);
  });
  it('previews the exact dated adjustment and hides it once balances match',()=>{
    const f=render('CreditCard');const c=f.componentInstance;c.check.set({...check,matches:false,difference:50,statementBalance:950});c.adjustmentOpen=true;f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Change in amount owed');expect(f.nativeElement.textContent).toContain('$950.00');
    c.changed();expect(c.adjustmentOpen).toBe(false);c.check.set(check);f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('Create adjustment and reconcile');
  });

});
