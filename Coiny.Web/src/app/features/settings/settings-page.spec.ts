import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SettingsPage } from './settings-page';

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>; let http: HttpTestingController;
  let token: string | null;
  beforeEach(async () => {
    token = 'session';
    TestBed.configureTestingModule({imports: [SettingsPage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      {provide: AuthService, useValue: {getToken: () => token, updateNetWorthPreference: vi.fn()}}]});
    fixture = TestBed.createComponent(SettingsPage); http = TestBed.inject(HttpTestingController); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({id: 1, name: 'Our household', role: 'Member'});
    http.expectOne(r => r.url.endsWith('/users/me/preferences')).flush({includeMortgageInNetWorth: true});
    http.expectOne(r => r.url.endsWith('/auth/sessions')).flush([]);
    http.expectOne(r => r.url.endsWith('/auth/recovery-codes')).flush({remaining: 0});
    await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/households/members')).flush([{id: 1, firstName: 'Alex', email: 'alex@example.test', role: 'Member', isCurrentUser: true}]);
    fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/recurring-expenses/settings')).flush({enabled: false, lastScanDate: null, editVersion: 4});
    fixture.detectChanges(); await fixture.whenStable();
    await vi.waitFor(() => expect(fixture.componentInstance.household?.loading()).toBe(false)); fixture.detectChanges();
  });
  afterEach(() => {http.verify(); vi.restoreAllMocks();});
  it('shows Household, Preferences, and Security together without a modal', () => {
    const headings = [...fixture.nativeElement.querySelectorAll('.section-heading h2')].map((node: any) => node.textContent.trim());
    expect(headings).toEqual(['Household', 'Preferences', 'Security']);
    expect(fixture.nativeElement.textContent).toContain('Our household');
    expect(fixture.nativeElement.textContent).toContain('Recovery codes');
    expect(fixture.nativeElement.querySelector('dialog')).toBeNull();
  });
  it('stays on the page after saving and reloads the version for the next edit', async () => {
    const preferences = fixture.componentInstance.preferences!; preferences.recurring!.enabled = true;
    preferences.save();
    const first = http.expectOne(r => r.method === 'PUT'); expect(first.request.body.recurringDetection).toEqual({enabled: true, expectedEditVersion: 4});
    first.flush({includeMortgageInNetWorth: true}); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/users/me/preferences')).flush({includeMortgageInNetWorth: true}); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/recurring-expenses/settings')).flush({enabled: true, lastScanDate: null, editVersion: 5});
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelectorAll('.settings-section')).toHaveLength(3); expect(preferences.hasChanges).toBe(false);
    preferences.recurring!.enabled = false; preferences.save();
    const second = http.expectOne(r => r.method === 'PUT'); expect(second.request.body.recurringDetection).toEqual({enabled: false, expectedEditVersion: 5});
    second.flush({}, {status: 409, statusText: 'Conflict'}); expect(preferences.hasChanges).toBe(true);
  });
  it('resets unsaved preferences without writing or leaving the page', async () => {
    const preferences = fixture.componentInstance.preferences!;
    fixture.nativeElement.querySelector('app-user-settings .preference-toggle').click();
    fixture.nativeElement.querySelector('app-recurring-settings input').click();
    await fixture.whenStable(); fixture.detectChanges(); const reset = [...fixture.nativeElement.querySelectorAll('app-user-settings button')].find((button: any) => button.textContent.trim() === 'Reset changes') as HTMLButtonElement;
    reset.click(); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/users/me/preferences')).flush({includeMortgageInNetWorth: true}); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/recurring-expenses/settings')).flush({enabled: false, lastScanDate: null, editVersion: 4});
    fixture.detectChanges(); await fixture.whenStable(); http.expectNone(r => r.method === 'PUT');
    expect(preferences.hasChanges).toBe(false); expect(preferences.includeMortgage).toBe(true);
  });
  it('protects pending changes and operations while allowing mandatory sign-out', () => {
    const page = fixture.componentInstance; const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(page.canLeave()).toBe(true); page.preferences!.includeMortgage = false; expect(page.canLeave()).toBe(false);
    expect(confirm).toHaveBeenCalledWith('Leave Settings? Your unsaved preferences will be lost.');
    page.preferences!.includeMortgage = true; page.security!.recoveryCodes.set(['unsaved-code']); expect(page.canLeave()).toBe(false);
    expect(confirm).toHaveBeenLastCalledWith('Leave Settings? Newly generated recovery codes won’t be shown again.');
    page.security!.recoveryCodes.set([]); page.household!.busy.set(true); expect(page.canLeave()).toBe(false);
    token = null; expect(page.canLeave()).toBe(true);
  });
});
