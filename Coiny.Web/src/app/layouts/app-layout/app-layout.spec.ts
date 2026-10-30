import { Subject } from 'rxjs';
import { PageLoadingService } from '../../core/services/page-loading.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Event as RouterEvent, DefaultUrlSerializer, NavigationStart, NavigationEnd, NavigationCancel, UrlTree, Router, provideRouter } from '@angular/router';
import { AppLayout } from './app-layout';
describe('AppLayout', () => {
  it('renders the shared header and footer around routed content', () => {
    TestBed.configureTestingModule({ imports: [AppLayout], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(AppLayout);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({ id: 'alex', email: 'alex@example.test', firstName: 'Alex' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-header').closest('.page-body')).toBeNull();
    expect(fixture.nativeElement.querySelector('.page-content').hasAttribute('inert')).toBe(true);
    expect(fixture.nativeElement.querySelector('.page-loading img').getAttribute('src')).toBe('assets/icons/favicon.svg');
    expect(fixture.nativeElement.querySelector('app-footer')).toBeTruthy();
    http.verify();
  });
});

// Exercise the shared router subscription without rendering page-specific modals.
describe('AppLayout navigation loading', () => {
  function setup() {
    const events = new Subject<RouterEvent>();
    const serializer = new DefaultUrlSerializer();
    const router = { url: '/accounts?accountId=1', events, parseUrl: (url: string) => serializer.parse(url), serializeUrl: (tree: UrlTree) => serializer.serialize(tree) };
    const loading = { begin: vi.fn(), ready: vi.fn(), cancel: vi.fn() };
    TestBed.configureTestingModule({providers: [
      {provide: Router, useValue: router}, {provide: PageLoadingService, useValue: loading}
    ]});
    TestBed.runInInjectionContext(() => new AppLayout());
    events.next(new NavigationEnd(1, router.url, router.url));
    loading.begin.mockClear(); loading.ready.mockClear(); loading.cancel.mockClear();
    return {events, loading};
  }
  it('does not replay the overlay for modal refreshes, query changes or fragments', () => {
    const {events, loading} = setup();
    for (const url of ['/accounts?accountId=1', '/accounts?accountId=2', '/accounts?accountId=1#details']) {
      events.next(new NavigationStart(2, url));
      events.next(new NavigationEnd(2, url, url));
    }
    expect(loading.begin).not.toHaveBeenCalled();
    expect(loading.ready).not.toHaveBeenCalled();
    expect(loading.cancel).not.toHaveBeenCalled();
  });
  it('still starts and settles the overlay when navigating to a different page', () => {
    const {events, loading} = setup();
    events.next(new NavigationStart(3, '/budget'));
    expect(loading.begin).toHaveBeenCalledWith(true);
    events.next(new NavigationEnd(3, '/budget', '/budget'));
    expect(loading.ready).toHaveBeenCalledOnce();
  });
  it('cleans up a cancelled page navigation', () => {
    const {events, loading} = setup();
    events.next(new NavigationStart(4, '/settings'));
    events.next(new NavigationCancel(4, '/settings', 'guard'));
    expect(loading.cancel).toHaveBeenCalledOnce();
  });
});
