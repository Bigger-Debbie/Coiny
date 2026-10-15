import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { authGuard } from './auth-guard';

describe('authGuard', () => {
  let auth: AuthService;
  let navigate: ReturnType<typeof vi.fn>;
  const check = () => TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/dashboard' } as RouterStateSnapshot));
  beforeEach(() => {
    const storage = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => storage.set(key, value),
      removeItem: (key: string) => storage.delete(key),
    });
    navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: { navigate } }] });
    auth = TestBed.inject(AuthService);
  });
  afterEach(() => vi.unstubAllGlobals());
  it('blocks a protected route after local logout', () => {
    const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }));
    auth.setToken(`e30.${payload}.signature`);
    expect(check()).toBe(true);
    auth.logout();
    expect(check()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
  it('rejects an expired token and removes it', () => {
    const payload = btoa(JSON.stringify({ exp: 1 }));
    auth.setToken(`e30.${payload}.signature`);
    expect(check()).toBe(false);
    expect(auth.getToken()).toBeNull();
  });
});
