import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
const user = { id: 'alex', email: 'alex@example.test', firstName: 'Alex' };
describe('AuthService shared profile', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  beforeEach(() => {
    const storage = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => storage.set(key, value),
      removeItem: (key: string) => storage.delete(key),
    });
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); vi.unstubAllGlobals(); });
  it('shares an in-flight profile and reuses the result for later subscribers', () => {
    const first = vi.fn(); const second = vi.fn(); const later = vi.fn();
    auth.getCurrentUser().subscribe(first);
    auth.getCurrentUser().subscribe(second);
    http.expectOne(r => r.url.endsWith('/auth/me')).flush(user);
    auth.getCurrentUser().subscribe(later);
    expect(first).toHaveBeenCalledWith(user);
    expect(second).toHaveBeenCalledWith(user);
    expect(later).toHaveBeenCalledWith(user);
    expect(auth.currentUser()).toEqual(user);
    http.expectNone(r => r.url.endsWith('/auth/me'));
  });
  it('renews the same user session without clearing the displayed profile', () => {
    auth.setToken('old-token'); auth.getCurrentUser().subscribe();
    http.expectOne(r=>r.url.endsWith('/auth/me')).flush(user);
    auth.renewSession('new-token');
    expect(auth.getToken()).toBe('new-token'); expect(auth.currentUser()).toEqual(user);
    auth.getCurrentUser().subscribe(); http.expectNone(r=>r.url.endsWith('/auth/me'));
  });
  it('cancels pending profile data on logout rather than allowing stale user state', () => {
    auth.setToken('old-token');
    const next = vi.fn();
    auth.getCurrentUser().subscribe(next);
    const pending = http.expectOne(r => r.url.endsWith('/auth/me'));
    auth.logout();
    expect(pending.cancelled).toBe(true);
    expect(next).not.toHaveBeenCalled();
    expect(auth.currentUser()).toBeNull();
    expect(auth.profileLoading()).toBe(false);
    expect(auth.getToken()).toBeNull();
  });
  it('invalidates a completed profile on the next login', () => {
    auth.getCurrentUser().subscribe();
    http.expectOne(r => r.url.endsWith('/auth/me')).flush(user);
    auth.setToken('new-token');
    expect(auth.currentUser()).toBeNull();
    auth.getCurrentUser().subscribe();
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({ ...user, firstName: 'Sam' });
    expect(auth.currentUser()?.firstName).toBe('Sam');
  });
  it('allows a later profile request to recover from failure', () => {
    auth.getCurrentUser().subscribe({ error: () => {} });
    http.expectOne(r => r.url.endsWith('/auth/me')).flush('Unavailable', { status: 500, statusText: 'Server error' });
    expect(auth.profileError()).toBe(true);
    auth.getCurrentUser().subscribe();
    http.expectOne(r => r.url.endsWith('/auth/me')).flush(user);
    expect(auth.profileError()).toBe(false);
    expect(auth.currentUser()).toEqual(user);
  });
});
