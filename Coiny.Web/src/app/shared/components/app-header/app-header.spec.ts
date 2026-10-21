import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AppHeader } from './app-header';

const profile = { id: 'alex', email: 'alex@example.test', firstName: 'Alex' };
describe('AppHeader profile menu', () => {
  let fixture: ComponentFixture<AppHeader>;
  let http: HttpTestingController;
  let auth: AuthService;
  let navigate: ReturnType<typeof vi.fn>;
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function(this: HTMLDialogElement) { this.setAttribute('open', ''); } });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function(this: HTMLDialogElement) { this.removeAttribute('open'); } });
  });
  afterAll(() => { Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype, 'close'); });
  beforeEach(() => {
    const storage = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => storage.set(key, value),
      removeItem: (key: string) => storage.delete(key),
    });
    TestBed.configureTestingModule({ imports: [AppHeader], providers: [
      provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
    ] });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
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

  it('opens Settings from the avatar menu and closes after saving preferences', async () => {
    loadProfile(); open();
    const settings = [...fixture.nativeElement.querySelectorAll('[role="menuitem"]')].find((item: unknown) => (item as HTMLElement).textContent?.trim() === 'Settings') as HTMLButtonElement;
    settings.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    http.expectOne(r => r.url.endsWith('/users/me/preferences')).flush({ includeMortgageInNetWorth: true }); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Settings');
    const toggle = fixture.nativeElement.querySelector('[role="switch"]') as HTMLInputElement; toggle.click(); fixture.detectChanges();
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const save = http.expectOne(r => r.method === 'PUT'); expect(save.request.body).toEqual({ includeMortgageInNetWorth: false });
    save.flush({ includeMortgageInNetWorth: false }); fixture.detectChanges();
    expect(auth.currentUser()?.includeMortgageInNetWorth).toBe(false); expect(fixture.nativeElement.querySelector('dialog')).toBeNull();
  });
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
  it('logs out locally, clears the profile, closes the menu and replaces the login route', async () => {
    loadProfile(); open();
    await fixture.componentInstance.logout(); fixture.detectChanges();
    expect(auth.getToken()).toBeNull();
    expect(auth.currentUser()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], { replaceUrl: true });
  });
  it('keeps the session when a page guard cancels logout navigation', async () => {
    loadProfile(); open(); navigate.mockResolvedValue(false);
    await fixture.componentInstance.logout();
    expect(auth.getToken()).toBe('demo-token');
    expect(auth.currentUser()?.firstName).toBe('Alex');
  });
  it('uses a generic avatar for missing names and keeps logout available after a profile error', () => {
    http.expectOne(r => r.url.endsWith('/auth/me')).flush('Unavailable', { status: 500, statusText: 'Server error' });
    fixture.detectChanges(); open();
    expect(avatar().textContent?.trim()).toBe('?');
    expect(fixture.nativeElement.textContent).toContain('Profile unavailable');
    expect(fixture.nativeElement.querySelector('.logout-button:last-child').textContent).toBe('Log out');
  });  it('opens Household as an in-place modal and returns focus to the avatar on close', async () => {
    loadProfile(); open(); fixture.nativeElement.querySelector('[role="menuitem"]').click(); fixture.detectChanges();
    expect(navigate).not.toHaveBeenCalled();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({ id: 1, name: 'Ross', role: 'Member' }); await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/members')).flush([{ id: 1, firstName: 'Alex', email: profile.email, role: 'Member', isCurrentUser: true }]);
    await vi.waitFor(() => expect(fixture.nativeElement.querySelector('dialog')).not.toBeNull()); fixture.detectChanges();
    const close = fixture.nativeElement.querySelector('[aria-label="Close dialog"]') as HTMLButtonElement;
    close.click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('dialog')).toBeNull(); expect(document.activeElement).toBe(avatar());
  });

  it('opens the compact navigation, includes every destination, and restores focus on Escape', () => {
    loadProfile();const toggle=fixture.nativeElement.querySelector('.mobile-toggle') as HTMLButtonElement;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');toggle.click();fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.navigation-link'));
    const mobile=fixture.nativeElement.querySelector('.mobile-profile');
    for(const text of ['Alex','alex@example.test','Household','Categories','Settings','Log out'])expect(mobile.textContent).toContain(text);
    document.activeElement!.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));fixture.detectChanges();
    expect(fixture.componentInstance.mobileOpen()).toBe(false);expect(document.activeElement).toBe(toggle);
  });
  it('closes mobile navigation on outside click, focus leaving, and route selection', () => {
    loadProfile();const c=fixture.componentInstance;c.openMobile();document.body.click();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);
    c.openMobile();const outside=document.createElement('button');document.body.append(outside);outside.focus();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);outside.remove();
    vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);
    c.openMobile();fixture.nativeElement.querySelector('.navigation-link').click();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);
  });
  it('opens Settings from mobile navigation and returns focus to its toggle on dismissal', async () => {
    loadProfile();const c=fixture.componentInstance;c.openMobile();fixture.detectChanges();
    const settings=Array.from(fixture.nativeElement.querySelectorAll('.mobile-profile button') as NodeListOf<HTMLButtonElement>).find(b=>b.textContent?.trim()==='Settings')!;
    settings.click();fixture.detectChanges();await fixture.whenStable();
    expect(c.mobileOpen()).toBe(false);http.expectOne(r=>r.url.endsWith('/users/me/preferences')).flush({includeMortgageInNetWorth:true});fixture.detectChanges();await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Settings');
    fixture.nativeElement.querySelector('[aria-label="Close dialog"]').click();fixture.detectChanges();expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.mobile-toggle'));
  });
  it('clears mobile state when resizing to desktop', () => {
    loadProfile();const c=fixture.componentInstance;c.openMobile();vi.stubGlobal('innerWidth',1024);c.onResize();expect(c.mobileOpen()).toBe(false);
  });

});
