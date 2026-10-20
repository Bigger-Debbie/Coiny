import { TestBed } from '@angular/core/testing';
import { ToastService, notificationSignal } from './toast.service';

describe('ToastService', () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { TestBed.resetTestingModule(); vi.useRealTimers(); });
  it('stacks messages and expires each independently after 30 seconds', () => {
    const service = TestBed.inject(ToastService);
    service.success('Created.'); vi.advanceTimersByTime(10_000); service.error('Failed.');
    expect(service.messages().map(t => t.kind)).toEqual(['success', 'error']);
    vi.advanceTimersByTime(19_999); expect(service.messages()).toHaveLength(2);
    vi.advanceTimersByTime(1); expect(service.messages().map(t => t.message)).toEqual(['Failed.']);
    vi.advanceTimersByTime(10_000); expect(service.messages()).toEqual([]);
  });
  it('dismisses only the chosen toast and ignores empty feedback', () => {
    const service = TestBed.inject(ToastService);
    service.success('Saved.'); service.success('Saved.'); service.error(' ');
    service.dismiss(service.messages()[0].id);
    expect(service.messages()).toHaveLength(1);
    vi.advanceTimersByTime(30_000); expect(service.messages()).toEqual([]);
  });
  it('announces consecutive identical action results but not resets', () => {
    const message = TestBed.runInInjectionContext(() => notificationSignal('success'));
    message.set('Saved.'); message.set(''); message.set('Saved.');
    expect(message()).toBe('Saved.'); expect(TestBed.inject(ToastService).messages()).toHaveLength(2);
  });
});
