import { TestBed } from '@angular/core/testing';
import { PageLoadingService } from './page-loading.service';
describe('PageLoadingService', () => {
  let loading: PageLoadingService;
  beforeEach(() => { vi.useFakeTimers(); TestBed.configureTestingModule({}); loading = TestBed.inject(PageLoadingService); });
  afterEach(() => { loading.cancel(); vi.useRealTimers(); });
  it('holds a fast page load for 600 milliseconds and fades before uncovering it', () => {
    loading.begin(); const request = loading.requestStarted(); loading.requestFinished(request);
    vi.advanceTimersByTime(599); expect(loading.visible()).toBe(true); expect(loading.fading()).toBe(false);
    vi.advanceTimersByTime(1); expect(loading.fading()).toBe(true);
    vi.advanceTimersByTime(450); expect(loading.visible()).toBe(false);
  });
  it('waits for navigation, requests, and a quiet render interval', () => {
    loading.begin(true); const request = loading.requestStarted(); vi.advanceTimersByTime(1100);
    loading.requestFinished(request); vi.advanceTimersByTime(200); expect(loading.fading()).toBe(false);
    loading.ready(); vi.advanceTimersByTime(149); expect(loading.fading()).toBe(false);
    vi.advanceTimersByTime(1); expect(loading.fading()).toBe(true);
  });
  it('keeps chained requests covered even when one arrives during the fade', () => {
    loading.begin(); vi.advanceTimersByTime(600); expect(loading.fading()).toBe(true);
    const request = loading.requestStarted(); expect(loading.fading()).toBe(false);
    vi.advanceTimersByTime(500); expect(loading.visible()).toBe(true);
    loading.requestFinished(request); vi.advanceTimersByTime(150); expect(loading.fading()).toBe(true);
    vi.advanceTimersByTime(450); expect(loading.visible()).toBe(false);
  });
  it('uncovers hung requests after the safety deadline without extending it', () => {
    loading.begin(); loading.requestStarted(); vi.advanceTimersByTime(8000); expect(loading.fading()).toBe(true);
    loading.requestStarted(); vi.advanceTimersByTime(450); expect(loading.visible()).toBe(false);
    expect(loading.visible()).toBe(false);
  });
  it('starts a fresh cycle when navigation changes during an earlier fade', () => {
    loading.begin(); vi.advanceTimersByTime(600); loading.begin(true); vi.advanceTimersByTime(450);
    expect(loading.visible()).toBe(true); expect(loading.fading()).toBe(false);
    loading.ready(); vi.advanceTimersByTime(150); expect(loading.fading()).toBe(true);
  });
  it('does not show an overlay for later background or interactive requests', () => {
    const request = loading.requestStarted(); loading.requestFinished(request); vi.advanceTimersByTime(2000); expect(loading.visible()).toBe(false);
    loading.begin(true); loading.cancel(); vi.advanceTimersByTime(10000); expect(loading.visible()).toBe(false);
  });
  it('ignores late completion from an older route while the new route is loading', () => {
    loading.begin(); const old = loading.requestStarted(); loading.begin(true); const current = loading.requestStarted();
    loading.ready(); loading.requestFinished(old); vi.advanceTimersByTime(1200); expect(loading.fading()).toBe(false);
    loading.requestFinished(current); vi.advanceTimersByTime(150); expect(loading.fading()).toBe(true);
  });

});
