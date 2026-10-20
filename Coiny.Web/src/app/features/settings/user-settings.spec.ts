import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UserSettings } from './user-settings';
import { UserPreferencesService } from '../../core/services/user-preferences.service';
describe('UserSettings', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [UserSettings], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('loads the saved preference and publishes changes only after a successful save', async () => {
    const f = TestBed.createComponent(UserSettings); f.detectChanges();
    http.expectOne(r => r.method === 'GET').flush({ includeMortgageInNetWorth: true }); f.detectChanges(); await f.whenStable();
    const toggle = f.nativeElement.querySelector('[role="switch"]') as HTMLInputElement; expect(toggle.checked).toBe(true);
    const changed = vi.fn(); TestBed.inject(UserPreferencesService).changed.subscribe(changed);
    toggle.click(); f.detectChanges(); expect(f.componentInstance.includeMortgage).toBe(false); expect(changed).not.toHaveBeenCalled();
    f.componentInstance.save(); const request = http.expectOne(r => r.method === 'PUT'); expect(request.request.body).toEqual({ includeMortgageInNetWorth: false });
    request.flush({ includeMortgageInNetWorth: false }); expect(changed).toHaveBeenCalledOnce();
  });
  it('keeps the modal usable after a save error and allows cancelling without saving', () => {
    const f = TestBed.createComponent(UserSettings); f.detectChanges(); http.expectOne(r => r.method === 'GET').flush({ includeMortgageInNetWorth: false });
    const saved = vi.fn(); f.componentInstance.saved.subscribe(saved); f.componentInstance.save();
    http.expectOne(r => r.method === 'PUT').flush({}, { status: 500, statusText: 'Unavailable' }); f.detectChanges();
    expect(f.componentInstance.busy()).toBe(false); expect(saved).not.toHaveBeenCalled(); expect(f.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    const cancelled = vi.fn(); f.componentInstance.cancelled.subscribe(cancelled);
    f.nativeElement.querySelector('button[type="button"]').click(); expect(cancelled).toHaveBeenCalledOnce(); http.expectNone(r => r.method === 'PUT');
  });
});
