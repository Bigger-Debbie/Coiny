import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PageLoadingService } from '../services/page-loading.service';
import { pageLoadingInterceptor } from './page-loading-interceptor';
import { environemnt } from '../../../environments/environment';
describe('pageLoadingInterceptor', () => {
  let client: HttpClient; let http: HttpTestingController;
  const started = vi.fn(); const finished = vi.fn();
  beforeEach(() => {
    started.mockReset(); finished.mockReset();
    TestBed.configureTestingModule({providers: [provideHttpClient(withInterceptors([pageLoadingInterceptor])), provideHttpClientTesting(),
      {provide: PageLoadingService, useValue: {requestStarted: started, requestFinished: finished}}]});
    client = TestBed.inject(HttpClient); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('finishes tracking successful and failed page requests', () => {
    client.get(environemnt.apiUrl + '/accounts').subscribe(); expect(started).toHaveBeenCalledTimes(1);
    http.expectOne(environemnt.apiUrl + '/accounts').flush([]); expect(finished).toHaveBeenCalledTimes(1);
    client.get(environemnt.apiUrl + '/budget').subscribe({error: () => {}});
    http.expectOne(environemnt.apiUrl + '/budget').flush({}, {status: 500, statusText: 'Unavailable'});
    expect(finished).toHaveBeenCalledTimes(2);
  });
  it('releases canceled requests and does not track unsubscribed observables', () => {
    client.get(environemnt.apiUrl + '/accounts'); expect(started).not.toHaveBeenCalled();
    const subscription = client.get(environemnt.apiUrl + '/accounts').subscribe();
    const request = http.expectOne(environemnt.apiUrl + '/accounts'); subscription.unsubscribe();
    expect(request.cancelled).toBe(true); expect(finished).toHaveBeenCalledOnce();
  });
  it('tracks page initialization writes but ignores unrelated requests', () => {
    client.post(environemnt.apiUrl + '/transactions', {}).subscribe(); http.expectOne(environemnt.apiUrl + '/transactions').flush({});
    client.get('assets/icons/Coiny-Coin-Outline.svg').subscribe(); http.expectOne('assets/icons/Coiny-Coin-Outline.svg').flush('');
    expect(started).toHaveBeenCalledOnce(); expect(finished).toHaveBeenCalledOnce();
  });
});
