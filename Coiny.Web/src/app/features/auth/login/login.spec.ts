import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let response: Subject<{ token: string }>;
  const auth = { login: vi.fn(), setToken: vi.fn() };

  beforeEach(async () => {
    vi.clearAllMocks();
    response = new Subject();
    auth.login.mockReturnValue(response);
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    }).compileComponents();
    fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
  });

  async function fillForm() {
    for (const [id, value] of [['email', 'alex@coiny.test'], ['password', 'example-password']]) {
      const input = fixture.nativeElement.querySelector('#' + id) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    await fixture.whenStable();
  }

  async function submit() {
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
  }

  it('toggles password visibility without submitting', async () => {
    const toggle = fixture.nativeElement.querySelector('.password-toggle') as HTMLButtonElement;
    toggle.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#password').type).toBe('text');
    expect(toggle.getAttribute('aria-label')).toBe('Hide password');
    toggle.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#password').type).toBe('password');
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('blocks invalid submissions and shows field feedback', async () => {
    await submit();
    expect(auth.login).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Enter a valid email address.');
    expect(fixture.nativeElement.textContent).toContain('Enter your password.');
  });

  it('prevents duplicate requests and navigates after success', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await fillForm();
    await submit();
    await submit();
    expect(auth.login).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.querySelector('.login-button').disabled).toBe(true);
    response.next({ token: 'new-token' });
    await fixture.whenStable();
    expect(auth.setToken).toHaveBeenCalledWith('new-token');
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('shows an error and allows retry after rejection', async () => {
    await fillForm();
    await submit();
    response.error({ status: 401 });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Email or password is incorrect');
    expect(fixture.nativeElement.querySelector('.login-button').disabled).toBe(false);
  });

  it('links to password recovery and can pause background motion', async () => {
    expect(fixture.nativeElement.querySelector('.forgot').getAttribute('href')).toBe('/forgot-password');
    expect(fixture.nativeElement.querySelector('.signup').getAttribute('href')).toBe('/signup');
    fixture.nativeElement.querySelector('.motion-control').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.coin-background').classList.contains('paused')).toBe(true);
  });
});
