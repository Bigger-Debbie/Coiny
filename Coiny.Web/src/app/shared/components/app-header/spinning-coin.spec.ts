import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { SpinningCoin } from './spinning-coin';

@Component({ imports: [SpinningCoin], template: '<span appSpinningCoin><img alt="" /></span>' })
class Host {}

describe('SpinningCoin', () => {
  let frame: FrameRequestCallback;
  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', vi.fn((callback: FrameRequestCallback) => { frame = callback; return 1; }));
    vi.stubGlobal('cancelAnimationFrame', vi.fn());
    vi.spyOn(performance, 'now').mockReturnValue(0);
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })));
  });
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
  function render() {
    const f = TestBed.createComponent(Host); f.detectChanges();
    const directive = f.debugElement.query(By.directive(SpinningCoin)).injector.get(SpinningCoin);
    return { f, directive, coin: f.nativeElement.querySelector('img') as HTMLImageElement };
  }
  function event(x: number, y = 0, pointerType = 'mouse') { return { clientX: x, clientY: y, pointerType } as PointerEvent; }
  it('follows a horizontal pass, slows down, and returns to the original face', () => {
    const { directive, coin } = render(); directive.enter(event(0)); directive.move(event(10));
    frame(300); const first = Number(coin.style.transform.match(/rotateY\(([^d]+)deg/)![1]);
    frame(600); const second = Number(coin.style.transform.match(/rotateY\(([^d]+)deg/)![1]);
    expect(first).toBeGreaterThan(0); expect(second - first).toBeLessThan(first);
    frame(1800); expect(coin.style.transform).toBe(''); expect(coin.style.willChange).toBe('');
    directive.leave(); directive.enter(event(10)); directive.move(event(0)); frame(300);
    expect(coin.style.transform).toMatch(/rotateY\(-/);
  });
  it('uses the vertical axis of movement and cancels its frame when destroyed', () => {
    const { f, directive, coin } = render(); directive.enter(event(0)); directive.move(event(0, 10)); frame(300);
    expect(coin.style.transform).toMatch(/rotateX\(-/); f.destroy(); expect(cancelAnimationFrame).toHaveBeenCalledWith(1);
  });
  it('stays still for touch and reduced-motion preferences', () => {
    const { directive } = render(); vi.mocked(requestAnimationFrame).mockClear(); directive.enter(event(0, 0, 'touch')); directive.move(event(10, 0, 'touch'));
    expect(requestAnimationFrame).not.toHaveBeenCalled();
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true })));
    directive.enter(event(0)); directive.move(event(10)); expect(requestAnimationFrame).not.toHaveBeenCalled();
  });
});
