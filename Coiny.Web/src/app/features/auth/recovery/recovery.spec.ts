import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { NgForm } from '@angular/forms';
import { PasswordRecovery } from './recovery';
const valid = {valid: true, invalid: false} as NgForm;
describe('PasswordRecovery', () => {
  let fixture: ComponentFixture<PasswordRecovery>; let http: HttpTestingController;
  async function create(reset = false, fragment = '', codeRecovery = false, emailEnabled = true) {
    TestBed.configureTestingModule({imports: [PasswordRecovery], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      {provide: ActivatedRoute, useValue: {snapshot: {routeConfig: {path: codeRecovery ? 'recover-with-code' : reset ? 'reset-password' : 'forgot-password'}, fragment}}}]});
    fixture = TestBed.createComponent(PasswordRecovery); http = TestBed.inject(HttpTestingController); fixture.detectChanges();
    if (!reset && !codeRecovery) http.expectOne(r => r.url.endsWith('/auth/recovery-options')).flush({email: emailEnabled, recoveryCode: true});
    fixture.detectChanges(); await fixture.whenStable();
  }
  afterEach(() => {http.verify(); vi.restoreAllMocks();});
  it('submits an email once and shows the generic confirmation', async () => {
    await create(); const c = fixture.componentInstance; c.email = 'alex@example.test'; c.submit(valid); c.submit(valid);
    const request = http.expectOne(r => r.url.endsWith('/forgot-password')); expect(request.request.body).toEqual({email: c.email});
    request.flush({message: 'If an account matches that email, a reset link will be sent.'}); fixture.detectChanges();
    expect(c.done()).toBe(true); expect(fixture.nativeElement.textContent).toContain('If an account matches'); expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });
  it('reads the fragment, removes it from history, and clears secrets after resetting', async () => {
    const replace = vi.spyOn(history, 'replaceState'); await create(true, 'email=alex%40example.test&token=secret%2Btoken');
    expect(replace).toHaveBeenCalled(); expect(String(replace.mock.calls[0][2])).not.toContain('secret');
    const c = fixture.componentInstance; c.password = 'New-password1!'; c.confirmPassword = c.password; c.submit(valid);
    const request = http.expectOne(r => r.url.endsWith('/reset-password')); expect(request.request.body.token).toBe('secret+token');
    request.flush({message: 'Your password has been reset.'}); expect(c.password).toBe(''); expect(c.done()).toBe(true);
  });
  it('blocks mismatched passwords and shows an expired-link error', async () => {
    await create(true, 'email=alex%40example.test&token=expired'); const c = fixture.componentInstance;
    c.password = 'New-password1!'; c.confirmPassword = 'different'; c.submit(valid); http.expectNone(r => r.method === 'POST');
    expect(c.error()).toContain('match'); c.confirmPassword = c.password; c.submit(valid);
    http.expectOne(r => r.url.endsWith('/reset-password')).flush({message: 'This reset link is invalid or expired.'}, {status: 400, statusText: 'Bad Request'});
    expect(c.error()).toContain('expired'); expect(c.done()).toBe(false); expect(c.busy()).toBe(false);
  });
  it('offers recovery codes without an email form when SMTP is unavailable', async () => {
    await create(false, '', false, false);
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    const link = fixture.nativeElement.querySelector('.recovery-code-link'); expect(link.getAttribute('href')).toBe('/recover-with-code');
    expect(fixture.nativeElement.textContent).toContain('Email recovery isn’t enabled');
    fixture.componentInstance.email = 'alex@example.test'; fixture.componentInstance.submit(valid); http.expectNone(r => r.method === 'POST');
  });
  it('resets with a saved code without email and clears the code after success', async () => {
    await create(false, '', true); const c = fixture.componentInstance;
    c.email = 'alex@example.test'; c.recoveryCode = 'saved-code'; c.password = c.confirmPassword = 'New-password1!';
    c.submit(valid); const request = http.expectOne(r => r.url.endsWith('/recover-with-code'));
    expect(request.request.body).toEqual({email: c.email, recoveryCode: 'saved-code', newPassword: c.password});
    request.flush({message: 'Your password has been reset.'}); fixture.detectChanges();
    expect(c.recoveryCode).toBe(''); expect(c.password).toBe(''); expect(c.done()).toBe(true); expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });
  it('keeps a rejected recovery form available and reports the generic error', async () => {
    await create(false, '', true); const c = fixture.componentInstance;
    c.email = 'alex@example.test'; c.recoveryCode = 'invalid-code'; c.password = c.confirmPassword = 'New-password1!'; c.submit(valid);
    http.expectOne(r => r.url.endsWith('/recover-with-code')).flush({message: 'The email or recovery code is incorrect.'}, {status: 400, statusText: 'Bad Request'});
    expect(c.error()).toContain('incorrect'); expect(c.done()).toBe(false); expect(c.busy()).toBe(false);
  });

});
