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
  it('blocks empty names and negative opening balances', async () => {
    const f = await render();
    Object.assign(f.componentInstance.model, { name: '', openingBalance: -1 });
    f.detectChanges(); await f.whenStable();
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await f.whenStable();
    http.expectNone(r => r.method === 'POST');
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
});
