import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { NgForm } from '@angular/forms';
import { Signup } from './signup';
import { AuthService } from '../../../core/services/auth.service';

describe('Signup', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Signup], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ code: 'K7MP-4X9Q' }) } } }] });
    http = TestBed.inject(HttpTestingController); vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => http.verify());
  it('validates email while typing and clears the label warning after correction', async () => {
    const f = TestBed.createComponent(Signup); f.detectChanges(); await f.whenStable();
    const input = f.nativeElement.querySelector('#signup-email') as HTMLInputElement;
    expect(f.nativeElement.querySelector('#signup-email-error').textContent.trim()).toBe('');
    input.value = 'alextros'; input.dispatchEvent(new Event('input')); f.detectChanges(); await f.whenStable(); f.detectChanges();
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(f.nativeElement.querySelector('.field-label #signup-email-error').textContent).toContain('Enter a valid email');
    input.value = 'alex@example.test'; input.dispatchEvent(new Event('input')); f.detectChanges(); await f.whenStable(); f.detectChanges();
    expect(input.getAttribute('aria-invalid')).toBe('false'); expect(f.nativeElement.querySelector('#signup-email-error').textContent.trim()).toBe('');
    http.expectNone(r => r.method === 'POST');
  });
  it('checks confirmation while typing and checks again when the original password changes', async () => {
    const f = TestBed.createComponent(Signup); f.detectChanges(); await f.whenStable();
    async function type(id: string, value: string) { const input = f.nativeElement.querySelector(id); input.value = value; input.dispatchEvent(new Event('input')); f.detectChanges(); await f.whenStable(); f.detectChanges(); }
    await type('#signup-password', 'CoinyNew!2026'); await type('#confirm-password', 'different');
    const confirmation = f.nativeElement.querySelector('#confirm-password');
    expect(confirmation.getAttribute('aria-invalid')).toBe('true');
    expect(f.nativeElement.querySelector('.field-label #confirm-password-error').textContent).toContain('Passwords don’t match');
    await type('#confirm-password', 'CoinyNew!2026'); expect(confirmation.getAttribute('aria-invalid')).toBe('false');
    await type('#signup-password', 'Changed!2026'); expect(confirmation.getAttribute('aria-invalid')).toBe('true');
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true })); f.detectChanges();
    http.expectNone(r => r.method === 'POST');
  });
  it('registers only matching strong passwords, stores the token, and preserves the invitation link', () => {
    const f = TestBed.createComponent(Signup); f.detectChanges(); const c = f.componentInstance;
    c.model = { firstName: ' Alex ', email: 'alex@example.test', password: 'CoinyNew!2026', confirmPassword: 'different' };
    c.signup({ invalid: false } as NgForm); http.expectNone(r => r.url.endsWith('/register'));
    c.model.confirmPassword = c.model.password;
    const token = vi.spyOn(TestBed.inject(AuthService), 'setToken').mockImplementation(() => {});
    c.signup({ invalid: false } as NgForm); c.signup({ invalid: false } as NgForm);
    const request = http.expectOne(r => r.url.endsWith('/register'));
    expect(request.request.body.firstName).toBe('Alex'); expect(request.request.body.confirmPassword).toBeUndefined();
    request.flush({ token: 'new-token', email: c.model.email });
    expect(token).toHaveBeenCalledWith('new-token');
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/household-setup'], { queryParams: { code: 'K7MP-4X9Q' } });
  });
  it('shows registration errors and allows corrections', () => {
    const f = TestBed.createComponent(Signup); f.detectChanges(); const c = f.componentInstance;
    c.model = { firstName: 'Alex', email: 'alex@example.test', password: 'CoinyNew!2026', confirmPassword: 'CoinyNew!2026' };
    c.signup({ invalid: false } as NgForm);
    http.expectOne(r => r.url.endsWith('/register')).flush([{ description: 'This email is already registered.' }], { status: 400, statusText: 'Invalid' });
    f.detectChanges(); expect(f.nativeElement.textContent).toContain('This email is already registered.'); expect(c.saving()).toBe(false);
  });
});
