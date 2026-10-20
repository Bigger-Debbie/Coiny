import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { CreateAccount } from './create-account';

describe('CreateAccount', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CreateAccount], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => http.verify());
  async function render() {
    const f = TestBed.createComponent(CreateAccount); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/institutions')).flush([{ id: 1, name: 'Bank', isActive: true }]);
    f.detectChanges(); await f.whenStable();
    return f;
  }
  it('loads an existing account and saves changes with PUT while keeping account type fixed', async () => {
    const f = TestBed.createComponent(CreateAccount);
    const account = { id: 8, name: 'Home', institutionId: 1, institutionName: 'Bank', accountType: 'Loan', loanSubtype: 'Mortgage', openingBalance: 100000, openingBalanceDate: '2026-01-01', currentBalance: 99000, isActive: true };
    f.componentRef.setInput('account', account); f.componentRef.setInput('embedded', true); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/institutions')).flush([{ id: 1, name: 'Bank', isActive: true }]);
    f.detectChanges(); await f.whenStable();
    expect(f.componentInstance.model.name).toBe('Home');
    const saved = vi.fn(); f.componentInstance.saved.subscribe(saved);
    f.componentInstance.model.name = 'Our home';
    f.componentInstance.save({ invalid: false } as never);
    const update = http.expectOne(r => r.method === 'PUT' && r.url.endsWith('/accounts/8'));
    expect(update.request.body.accountType).toBe('Loan'); expect(update.request.body.name).toBe('Our home');
    update.flush({ ...account, name: 'Our home' }); expect(saved).toHaveBeenCalled();
  });
  it('blocks empty names and negative opening balances', async () => {
    const f = await render();
    Object.assign(f.componentInstance.model, { name: '', openingBalance: -1 });
    f.detectChanges(); await f.whenStable();
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await f.whenStable();
    http.expectNone(r => r.method === 'POST');
  });
  it('offers loan subtypes and sends the selected mortgage classification', async () => {
    const f = await render(); Object.assign(f.componentInstance.model, { name: 'Home loan', accountType: 'Loan', loanSubtype: 'Mortgage', openingBalance: 1000, openingBalanceDate: '2026-01-01' });
    f.detectChanges(); await f.whenStable(); expect(f.nativeElement.querySelector('#loan-subtype')).toBeTruthy();
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); await f.whenStable();
    const request = http.expectOne(r => r.method === 'POST'); expect(request.request.body).toMatchObject({ accountType: 'Loan', loanSubtype: 'Mortgage' }); request.flush({ id: 9 });
  });
  it('creates an account with its opening date and navigates to it', async () => {
    const f = await render();
    Object.assign(f.componentInstance.model, { name: ' New savings ', openingBalance: 500, openingBalanceDate: '2026-01-01' });
    f.detectChanges(); await f.whenStable();
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await f.whenStable();
    const request = http.expectOne(r => r.method === 'POST');
    expect(request.request.body).toMatchObject({ name: 'New savings', openingBalance: 500, openingBalanceDate: '2026-01-01', institutionId: null });
    request.flush({ id: 9 });
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/accounts'], { queryParams: { accountId: 9 } });
  });
  it('creates and selects an institution while preserving the account draft and blocking dismissal', async () => {
    const f = await render();
    const c = f.componentInstance;
    c.model.name = 'My checking';
    c.addingInstitution.set(true);
    c.institutionName = ' New Bank ';
    c.institutionWebsite = ' https://bank.example ';
    c.saveInstitution(); c.saveInstitution();
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/institutions'));
    expect(request.request.body).toEqual({ name: 'New Bank', website: 'https://bank.example' });
    expect(c.busy()).toBe(true);
    c.closeInstitution(); expect(c.addingInstitution()).toBe(true);
    request.flush({ id: 2, name: 'New Bank', website: 'https://bank.example', isActive: true });
    expect(c.model.institutionId).toBe(2);
    expect(c.model.name).toBe('My checking');
    expect(c.addingInstitution()).toBe(false);
    expect(c.busy()).toBe(false);
  });
  it('validates institution details and retains the draft after a failed save', async () => {
    const f = await render(); const c = f.componentInstance;
    c.saveInstitution();
    expect(c.institutionError()).toContain('name');
    c.institutionName = 'Bank'; c.institutionWebsite = 'javascript:alert(1)'; c.saveInstitution();
    http.expectNone(r => r.method === 'POST');
    c.institutionWebsite = ''; c.saveInstitution();
    http.expectOne(r => r.method === 'POST').flush({}, { status: 500, statusText: 'Error' });
    expect(c.institutionName).toBe('Bank');
    expect(c.institutionError()).toContain('try again');
    expect(c.busy()).toBe(false);
  });
});
