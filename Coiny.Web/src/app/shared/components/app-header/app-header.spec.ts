import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AppHeader } from './app-header';

const profile = { id: 'alex', email: 'alex@example.test', firstName: 'Alex' };
describe('AppHeader profile menu', () => {
  let fixture: ComponentFixture<AppHeader>;
  let http: HttpTestingController;
  let auth: AuthService;
  let navigate: ReturnType<typeof vi.fn>;
  beforeEach(() => {
    const storage = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => storage.set(key, value),
      removeItem: (key: string) => storage.delete(key),
    });
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({ imports: [AppHeader], providers: [
      provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: { navigate } },
    ] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.setToken('demo-token');
    fixture = TestBed.createComponent(AppHeader);
    fixture.detectChanges();
  });
  afterEach(() => { http.verify(); vi.unstubAllGlobals(); });
  function loadProfile(firstName: string | null = 'Alex') {
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({ ...profile, firstName });
    fixture.detectChanges();
  }
  function avatar(): HTMLButtonElement { return fixture.nativeElement.querySelector('.avatar-button'); }
  function open() { avatar().click(); fixture.detectChanges(); }

  it('shows the current initial, name and email and toggles the menu', () => {
    loadProfile();
    expect(avatar().textContent?.trim()).toBe('A');
    expect(avatar().getAttribute('aria-expanded')).toBe('false');
    open();
    expect(avatar().getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role="menu"]').textContent).toContain('alex@example.test');
    expect(fixture.nativeElement.querySelector('.profile-details strong').textContent).toBe('Alex');
    avatar().click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
  });
  it('opens with ArrowDown, focuses logout, and returns focus on Escape', () => {
    loadProfile();
    avatar().dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[role="menuitem"]'));
    document.activeElement!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(avatar());
  });
  it('closes on outside click and when focus leaves the profile controls', () => {
    loadProfile(); open();
    document.body.click(); fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    open();
    const outside = document.createElement('button');
    document.body.append(outside); outside.focus();
    fixture.detectChanges();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    outside.remove();
  });
  it('logs out locally, clears the profile, closes the menu and replaces the login route', () => {
    loadProfile(); open();
    fixture.nativeElement.querySelector('[role="menuitem"]').click(); fixture.detectChanges();
    expect(auth.getToken()).toBeNull();
    expect(auth.currentUser()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });
  it('uses a generic avatar for missing names and keeps logout available after a profile error', () => {
    http.expectOne(r => r.url.endsWith('/auth/me')).flush('Unavailable', { status: 500, statusText: 'Server error' });
    fixture.detectChanges(); open();
    expect(avatar().textContent?.trim()).toBe('?');
    expect(fixture.nativeElement.textContent).toContain('Profile unavailable');
    expect(fixture.nativeElement.querySelector('[role="menuitem"]').textContent).toBe('Log out');
  });
});
