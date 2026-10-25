import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AlertsService } from './alerts.service';
describe('Alerts polling',()=>{
  afterEach(()=>{vi.useRealTimers();});
  it('polls authenticated counts, preserves count on errors, and cancels pending requests on logout',()=>{
    vi.useFakeTimers();TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const http=TestBed.inject(HttpTestingController);const service=TestBed.inject(AlertsService);
    service.start();vi.advanceTimersByTime(0);http.expectOne(r=>r.url.endsWith('/unread-count')).flush({unreadCount:3});expect(service.unread()).toBe(3);
    vi.advanceTimersByTime(60000);http.expectOne(r=>r.url.endsWith('/unread-count')).flush({}, {status:500,statusText:'Unavailable'});expect(service.unread()).toBe(3);
    vi.advanceTimersByTime(60000);const pending=http.expectOne(r=>r.url.endsWith('/unread-count'));service.stop();expect(pending.cancelled).toBe(true);expect(service.unread()).toBe(0);
    vi.advanceTimersByTime(60000);http.expectNone(r=>r.url.endsWith('/unread-count'));http.verify();
  });
});
