import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MarketAdjustment } from './market-adjustment';

describe('MarketAdjustment', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [MarketAdjustment], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('shows the difference for a zero balance and reuses the dated baseline while typing', () => {
    const f = TestBed.createComponent(MarketAdjustment); const checked = vi.fn(); f.componentInstance.checked.subscribe(checked);
    f.componentRef.setInput('accountId', 2); f.componentRef.setInput('date', '2026-10-16'); f.componentRef.setInput('balance', 0); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/market-adjustments/balance')).flush({ balance: 1000 }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Market loss: $1,000.00'); expect(checked).toHaveBeenLastCalledWith(1000);
    f.componentRef.setInput('balance', 1250); f.detectChanges(); expect(f.nativeElement.textContent).toContain('Market gain: $250.00');
    http.expectNone(r => r.url.endsWith('/balance'));
  });
  it('cancels an outdated preview and blocks saving until the new date loads', () => {
    const f = TestBed.createComponent(MarketAdjustment); const checked = vi.fn(); f.componentInstance.checked.subscribe(checked);
    f.componentRef.setInput('accountId', 2); f.componentRef.setInput('date', '2026-10-16'); f.detectChanges();
    const old = http.expectOne(r => r.params.get('date') === '2026-10-16');
    f.componentRef.setInput('date', '2026-10-17'); f.detectChanges(); expect(old.cancelled).toBe(true); expect(checked).toHaveBeenLastCalledWith(null);
    http.expectOne(r => r.params.get('date') === '2026-10-17').flush({}, { status: 400, statusText: 'Bad Request' }); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Try again'); expect(f.componentInstance.baseline()).toBeNull();
    f.componentInstance.load(); http.expectOne(r => r.params.get('date') === '2026-10-17').flush({ balance: 900 });
    expect(checked).toHaveBeenLastCalledWith(900);
  });
});
