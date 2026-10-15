import { TestBed } from '@angular/core/testing';
import { TrendChart } from './trend-chart';
describe('TrendChart', () => {
  function render(points = [{ date: '2026-10-01', value: -100 }, { date: '2026-10-02', value: 0 }, { date: '2026-10-05', value: 100 }]) {
    const fixture = TestBed.createComponent(TrendChart);
    fixture.componentRef.setInput('points', points);
    fixture.detectChanges();
    return fixture;
  }
  it('shows zero crossings and spaces points by date without persistent labels', () => {
    const f = render();
    expect(f.nativeElement.querySelector('.zero-line')).toBeTruthy();
    expect(f.componentInstance.plot[1].x).toBe(210);
    expect(f.nativeElement.querySelector('.tooltip')).toBeNull();
  });
  it('draws a single dot and handles flat histories', () => {
    const f = render([{ date: '2026-10-01', value: 0 }]);
    expect(f.nativeElement.querySelector('.dot')).toBeTruthy();
    expect(f.nativeElement.querySelector('.line')).toBeNull();
    f.componentRef.setInput('points', [{ date: '2026-10-01', value: 100 }, { date: '2026-10-02', value: 100 }]);
    f.detectChanges();
    expect(f.componentInstance.plot[0].y).toBe(f.componentInstance.plot[1].y);
    expect(f.componentInstance.linePath).not.toContain('NaN');
  });
  it('supports keyboard exploration with an accessible date and amount', () => {
    const f = render();
    const control: HTMLElement = f.nativeElement.querySelector('[role="slider"]');
    control.dispatchEvent(new FocusEvent('focus'));
    control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    control.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight' }));
    f.detectChanges();
    expect(f.componentInstance.activeIndex).toBe(1);
    expect(control.getAttribute('aria-valuetext')).toContain('October 2, 2026');
    expect(f.nativeElement.querySelector('.tooltip').textContent).toContain('$0.00');
    control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    f.detectChanges();
    expect(f.nativeElement.querySelector('.tooltip')).toBeNull();
  });
  it('selects the nearest point on tap', () => {
    const f = render();
    const control: HTMLElement = f.nativeElement.querySelector('.chart');
    vi.spyOn(control, 'getBoundingClientRect').mockReturnValue({ left: 0, width: 800 } as DOMRect);
    control.dispatchEvent(new MouseEvent('pointerdown', { clientX: 205 }));
    f.detectChanges();
    expect(f.componentInstance.activeIndex).toBe(1);
  });
  it('clears geometry when the history becomes empty', () => {
    const f = render();
    f.componentRef.setInput('points', []);
    f.detectChanges();
    expect(f.componentInstance.plot).toEqual([]);
    expect(f.componentInstance.activePoint).toBeNull();
  });
});
