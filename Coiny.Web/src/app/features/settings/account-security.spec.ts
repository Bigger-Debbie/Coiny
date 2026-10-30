import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { NgForm } from '@angular/forms';
import { AccountSecurity } from './account-security';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';
const valid = {valid: true, invalid: false, resetForm: vi.fn()} as unknown as NgForm;
describe('AccountSecurity', () => {
  let fixture: ComponentFixture<AccountSecurity>; let http: HttpTestingController;
  const logout = vi.fn(); const renewSession = vi.fn(); const navigate = vi.fn();
  beforeEach(async () => {
    logout.mockReset(); renewSession.mockReset(); navigate.mockReset().mockResolvedValue(true);
    TestBed.configureTestingModule({imports: [AccountSecurity], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      {provide: AuthService, useValue: {logout, renewSession}}, {provide: Router, useValue: {navigate}}]});
    fixture = TestBed.createComponent(AccountSecurity); http = TestBed.inject(HttpTestingController); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/auth/sessions')).flush([
      {id: 'this', device: 'Browser A', createdAt: '2026-10-28T12:00:00Z', expiresAt: '2026-10-28T14:00:00Z', isCurrent: true},
      {id: 'other', device: 'Browser B', createdAt: '2026-10-28T12:00:00Z', expiresAt: '2026-10-28T14:00:00Z', isCurrent: false}]);
    http.expectOne(r => r.url.endsWith('/auth/recovery-codes')).flush({remaining: 0});
    fixture.detectChanges(); await fixture.whenStable();
  });
  afterEach(() => http.verify());
  it('identifies this browser and confirms before revoking another session', () => {
    const rows = fixture.nativeElement.querySelectorAll('li'); expect(rows[0].textContent).toContain('This browser');
    expect(rows[0].querySelector('button')).toBeNull(); rows[1].querySelector('button').click(); fixture.detectChanges();
    http.expectNone(r => r.method === 'DELETE'); expect(fixture.nativeElement.querySelector('.confirmation').textContent).toContain('Browser B');
    fixture.componentInstance.revoke(); http.expectOne(r => r.url.endsWith('/sessions/other') && r.method === 'DELETE').flush(null);
    http.expectOne(r => r.url.endsWith('/sessions')).flush([]); expect(fixture.componentInstance.pendingSession()).toBeNull();
  });
  it('updates the password without leaving settings and refreshes the current session', () => {
    const c = fixture.componentInstance; c.currentPassword = 'old'; c.newPassword = 'New-password1!'; c.confirmPassword = 'wrong';
    c.changePassword(valid); http.expectNone(r => r.method === 'POST'); expect(c.error()).toContain('match');
    c.confirmPassword = c.newPassword; c.changePassword(valid); c.changePassword(valid);
    const request = http.expectOne(r => r.url.endsWith('/change-password')); expect(request.request.body.currentPassword).toBe('old');
    expect(logout).not.toHaveBeenCalled(); request.flush({token:'renewed-session',email:'alex@example.test'});
    expect(renewSession).toHaveBeenCalledWith('renewed-session'); expect(c.newPassword).toBe('');
    expect(logout).not.toHaveBeenCalled(); expect(navigate).not.toHaveBeenCalled();
    expect(c.codeCount()).toBe(0);
    expect(TestBed.inject(ToastService).messages().at(-1)?.message).toBe('Password updated successfully.');
    http.expectOne(r => r.url.endsWith('/sessions')).flush([{id:'renewed',isCurrent:true}]);
  });
  it('keeps password fields and the existing session when a change fails', () => {
    const c = fixture.componentInstance; c.currentPassword='old'; c.newPassword=c.confirmPassword='New-password1!';
    c.changePassword(valid);
    http.expectOne(r=>r.url.endsWith('/change-password')).flush({message:'Your current password could not be verified.'},{status:400,statusText:'Bad Request'});
    expect(c.busy()).toBe(false); expect(c.currentPassword).toBe('old');
    expect(c.error()).toContain('could not be verified');
    expect(renewSession).not.toHaveBeenCalled(); expect(logout).not.toHaveBeenCalled(); expect(navigate).not.toHaveBeenCalled();
    expect(TestBed.inject(ToastService).messages()).toHaveLength(0);
  });
  it('keeps the session on rejection and allows retry', () => {
    const c = fixture.componentInstance; c.signOutPassword = 'incorrect'; c.signOutAll(valid);
    http.expectOne(r => r.url.endsWith('/logout-all')).flush({message: 'Password could not be verified.'}, {status: 400, statusText: 'Bad Request'});
    expect(c.busy()).toBe(false); expect(c.error()).toContain('could not be verified'); expect(logout).not.toHaveBeenCalled();
  });
  it('requires a password and shows a generated set only until acknowledged', () => {
    const c = fixture.componentInstance;
    const form = {invalid: false, resetForm: vi.fn()} as unknown as NgForm;
    c.manageCodes({invalid: true} as NgForm); http.expectNone(r => r.method === 'POST');
    c.recoveryPassword = 'current-password'; c.manageCodes(form); c.manageCodes(form);
    const request = http.expectOne(r => r.url.endsWith('/auth/recovery-codes') && r.method === 'POST');
    expect(request.request.body).toEqual({currentPassword: 'current-password'});
    const codes = Array.from({length: 10}, (_, i) => `CODE-${i}`); request.flush({codes}); fixture.detectChanges();
    expect(c.codeCount()).toBe(10); expect(c.recoveryPassword).toBe(''); expect(form.resetForm).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('textarea').value).toBe(codes.join('\n'));
    const saved = [...fixture.nativeElement.querySelectorAll('button')].find((button: any) => button.textContent.includes('saved my codes')) as HTMLButtonElement;
    saved.click(); fixture.detectChanges(); expect(c.recoveryCodes()).toEqual([]); expect(fixture.nativeElement.querySelector('textarea')).toBeNull();
  });
  it('revokes codes without signing out and reloads status after an uncertain failure', () => {
    const c = fixture.componentInstance; c.recoveryPassword = 'current-password'; c.codeCount.set(10);
    const form = {invalid: false, resetForm: vi.fn()} as unknown as NgForm;
    c.manageCodes(form, true); http.expectOne(r => r.url.endsWith('/recovery-codes/revoke')).flush(null);
    expect(c.codeCount()).toBe(0); expect(logout).not.toHaveBeenCalled();
    c.manageCodes(form); http.expectOne(r => r.url.endsWith('/recovery-codes') && r.method === 'POST').flush({}, {status: 503, statusText: 'Unavailable'});
    http.expectOne(r => r.url.endsWith('/recovery-codes') && r.method === 'GET').flush({remaining: 10});
    expect(c.recoveryCodes()).toEqual([]); expect(c.error()).toContain('generate another set'); expect(c.busy()).toBe(false);
  });

  it('does not let an older status response overwrite a newly generated set', () => {
    const c = fixture.componentInstance; c.loadCodeStatus();
    const oldStatus = http.expectOne(r => r.url.endsWith('/recovery-codes') && r.method === 'GET');
    c.manageCodes({invalid: false, resetForm: vi.fn()} as unknown as NgForm);
    http.expectOne(r => r.url.endsWith('/recovery-codes') && r.method === 'POST').flush({codes: ['new-code']});
    oldStatus.flush({remaining: 0}); expect(c.codeCount()).toBe(1); expect(c.recoveryCodes()).toEqual(['new-code']);
  });

});
