import { AlertsService } from '../../../core/services/alerts.service';
import { signal } from '@angular/core';
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
      {provide:AlertsService,useValue:{unread:signal(0),start:vi.fn(),stop:vi.fn()}},
    ] });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.setToken('demo-token');
    fixture = TestBed.createComponent(AppHeader);
    fixture.detectChanges();
  });
  afterEach(() => { http.match(r=>r.url.endsWith('/recurring-expenses/settings') && r.method==='GET').filter(r=>!r.cancelled).forEach(r=>r.flush({enabled:false,lastScanDate:null,editVersion:0})); http.verify(); vi.unstubAllGlobals(); });
  function loadProfile(firstName: string | null = 'Alex') {
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({ ...profile, firstName });
    fixture.detectChanges();
  }
  function avatar(): HTMLButtonElement { return fixture.nativeElement.querySelector('.avatar-button'); }
  function open() { avatar().click(); fixture.detectChanges(); }

  it('shows a blue unread count on the avatar and mobile toggle and names it accessibly',()=>{
    loadProfile();const alerts=TestBed.inject(AlertsService);alerts.unread.set(3);fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.alert-badge')).toHaveLength(2);
    expect(avatar().getAttribute('aria-label')).toBe('Profile menu, 3 unread alerts');
    open();expect(fixture.nativeElement.querySelector('[role="menu"]').textContent).toContain('Alerts 3');
    expect(alerts.start).toHaveBeenCalledOnce();
  });
  it('opens institution cards from the avatar menu without navigating', async () => {
    loadProfile(); open();
    const item = [...fixture.nativeElement.querySelectorAll('[role="menuitem"]')].find((item: unknown) => (item as HTMLElement).textContent?.trim() === 'Institutions') as HTMLButtonElement;
    item.click(); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/institutions')).flush([{id:1,name:'Demo bank',website:null,isActive:true}]);
    http.expectOne(r => r.url.endsWith('/accounts')).flush([]); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('dialog').getAttribute('aria-label')).toBe('Institutions');
    expect(fixture.nativeElement.textContent).toContain('Demo bank'); expect(fixture.componentInstance.menuOpen()).toBe(false); expect(navigate).not.toHaveBeenCalled();
  });
  it('opens the Settings page from the avatar menu without a dialog', async () => {
    loadProfile(); open();
    const settings = [...fixture.nativeElement.querySelectorAll('[role="menuitem"]')].find((item: unknown) => (item as HTMLElement).textContent?.trim() === 'Settings') as HTMLButtonElement;
    settings.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.componentInstance.menuOpen()).toBe(false); expect(navigate).toHaveBeenCalledWith(['/settings']);
    expect(fixture.nativeElement.querySelector('dialog')).toBeNull();
    http.expectNone(r => r.url.endsWith('/users/me/preferences'));
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
    await fixture.componentInstance.logout();
    http.expectOne(r=>r.url.endsWith('/auth/logout') && r.method==='POST').flush(null);
    fixture.detectChanges();
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
  });  it('consolidates Household, Settings, and Security into one Settings destination', () => {
    loadProfile(); open();
    const labels = [...fixture.nativeElement.querySelectorAll('[role="menuitem"]')].map((item: any) => item.textContent.trim());
    expect(labels).toEqual(['Institutions', 'Categories', 'Merchant rules', 'Alerts', 'Settings', 'Log out']);
  });

  it('opens the compact navigation, includes every destination, and restores focus on Escape', () => {
    loadProfile();const toggle=fixture.nativeElement.querySelector('.mobile-toggle') as HTMLButtonElement;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');toggle.click();fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');expect(fixture.componentInstance.menuOpen()).toBe(false);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('.navigation-link'));
    const mobile=fixture.nativeElement.querySelector('.mobile-profile');
    for(const text of ['Alex','alex@example.test','Categories','Settings','Log out'])expect(mobile.textContent).toContain(text);
    document.activeElement!.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));fixture.detectChanges();
    expect(fixture.componentInstance.mobileOpen()).toBe(false);expect(document.activeElement).toBe(toggle);
  });
  it('closes mobile navigation on outside click, focus leaving, and route selection', () => {
    loadProfile();const c=fixture.componentInstance;c.openMobile();document.body.click();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);
    c.openMobile();const outside=document.createElement('button');document.body.append(outside);outside.focus();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);outside.remove();
    vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);
    c.openMobile();fixture.nativeElement.querySelector('.navigation-link').click();fixture.detectChanges();expect(c.mobileOpen()).toBe(false);
  });
  it('opens the same Settings page from mobile navigation and closes the menu', async () => {
    loadProfile(); const c = fixture.componentInstance; c.openMobile(); fixture.detectChanges();
    const settings = Array.from(fixture.nativeElement.querySelectorAll('.mobile-profile button') as NodeListOf<HTMLButtonElement>).find(b => b.textContent?.trim() === 'Settings')!;
    settings.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(c.mobileOpen()).toBe(false); expect(navigate).toHaveBeenCalledWith(['/settings']); expect(fixture.nativeElement.querySelector('dialog')).toBeNull();
    const labels = [...fixture.nativeElement.querySelectorAll('.mobile-profile button')].map((item: any) => item.textContent.trim());
    expect(labels).not.toContain('Household'); expect(labels).not.toContain('Security');
  });
  it('clears mobile state when resizing to desktop', () => {
    loadProfile();const c=fixture.componentInstance;c.openMobile();vi.stubGlobal('innerWidth',1024);c.onResize();expect(c.mobileOpen()).toBe(false);
  });

});
