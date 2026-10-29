import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from '../services/auth.service';
import { environemnt } from '../../../environments/environment';
describe('authInterceptor', () => {
  let http: HttpTestingController; let client: HttpClient; let token: string | null;
  const logout = vi.fn(); const navigate = vi.fn();
  beforeEach(() => {
    token = 'current-token'; logout.mockReset().mockImplementation(() => token = null); navigate.mockReset().mockResolvedValue(true);
    TestBed.configureTestingModule({providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([]),
      {provide: AuthService, useValue: {getToken: () => token, logout}}, {provide: Router, useValue: {navigate}}]});
    http = TestBed.inject(HttpTestingController); client = TestBed.inject(HttpClient);
  });
  afterEach(() => http.verify());
  it('adds tokens only to Coiny protected API requests', () => {
    client.get(environemnt.apiUrl + '/accounts').subscribe(); const request = http.expectOne(environemnt.apiUrl + '/accounts');
    expect(request.request.headers.get('Authorization')).toBe('Bearer current-token'); request.flush([]);
    for (const url of ['https://third-party.test/data', environemnt.apiUrl + '/auth/login']) {
      client.get(url).subscribe(); const publicRequest = http.expectOne(url); expect(publicRequest.request.headers.has('Authorization')).toBe(false); publicRequest.flush({});
    }
  });
  it('clears a revoked session and returns to login', () => {
    client.get(environemnt.apiUrl + '/accounts').subscribe({error: () => {}});
    http.expectOne(environemnt.apiUrl + '/accounts').flush({}, {status: 401, statusText: 'Unauthorized'});
    expect(logout).toHaveBeenCalledOnce(); expect(navigate).toHaveBeenCalledWith(['/login'], {replaceUrl: true});
  });
  it('does not clear a newer login when an old request fails', () => {
    client.get(environemnt.apiUrl + '/accounts').subscribe({error: () => {}}); token = 'new-token';
    http.expectOne(environemnt.apiUrl + '/accounts').flush({}, {status: 401, statusText: 'Unauthorized'});
    expect(logout).not.toHaveBeenCalled(); expect(navigate).not.toHaveBeenCalled();
  });
});
