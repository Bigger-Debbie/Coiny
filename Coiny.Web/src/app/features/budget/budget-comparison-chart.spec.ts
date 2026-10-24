import { TestBed } from '@angular/core/testing';
import { BudgetComparisonChart } from './budget-comparison-chart';
import { BudgetComparisonPoint } from '../../shared/models/budget/budget-actuals';

const points: BudgetComparisonPoint[] = [
  { date: '2026-10-01', plannedSpending: 100, actualSpending: 80, plannedContributions: 20, actualContributions: 0 },
  { date: '2026-10-02', plannedSpending: 200, actualSpending: 250, plannedContributions: 40, actualContributions: 30 },
  { date: '2026-10-03', plannedSpending: 300, actualSpending: null, plannedContributions: 60, actualContributions: null },
];
describe('BudgetComparisonChart', () => {
  function render(data = points) { const f = TestBed.createComponent(BudgetComparisonChart); f.componentRef.setInput('points', data); f.detectChanges(); return f; }
  it('renders both lines on one scale and stops actuals before future days', () => {
    const f = render(); const c = f.componentInstance;
    expect(c.scope).toBe('All'); expect(c.plot.map(p => p.actual)).toEqual([80, 280, null]); expect(c.actualPath.match(/[ML]/g)).toHaveLength(2);
    expect(f.nativeElement.querySelector('.planned-line')).toBeTruthy(); expect(f.nativeElement.querySelector('.actual-line')).toBeTruthy();
    expect(c.plot[1].actualY!).toBeLessThan(c.plot[1].plannedY); expect(c.plannedPath).not.toContain('NaN');
  });
  it('switches between spending, contribution, and combined amounts without refetching data', () => {
    const f = render(); const c = f.componentInstance;
    c.selectScope('Contributions'); expect(c.plot.map(p => p.planned)).toEqual([20, 40, 60]); expect(c.plot[1].actual).toBe(30);
    c.selectScope('All'); expect(c.plot.map(p => p.planned)).toEqual([120, 240, 360]); expect(c.plot[1].actual).toBe(280); expect(c.plot[2].actual).toBeNull();
  });
  it('supports keyboard exploration, future-day tooltips, and escape', () => {
    const f = render(); const slider = f.nativeElement.querySelector('[role="slider"]') as HTMLElement;
    slider.dispatchEvent(new FocusEvent('focus')); f.detectChanges(); expect(f.componentInstance.activeIndex).toBe(1);
    slider.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' })); f.detectChanges(); expect(slider.getAttribute('aria-valuetext')).toContain('no actuals yet'); expect(f.nativeElement.textContent).toContain('No actuals yet');
    slider.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' })); expect(f.componentInstance.activeIndex).toBe(0);
    slider.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); expect(f.componentInstance.activeIndex).toBeNull();
  });
  it('handles zero and future-only months without inventing an actual line', () => {
    const f = render(points.map(p => ({ ...p, plannedSpending: 0, plannedContributions: 0, actualSpending: null, actualContributions: null })));
    expect(f.componentInstance.plannedPath).not.toContain('NaN'); expect(f.nativeElement.querySelector('.actual-line')).toBeNull();
    f.componentRef.setInput('points', []); f.detectChanges(); expect(f.nativeElement.textContent).toContain('No daily comparison');
  });
  it('selects the nearest day by pointer and clears hover when leaving', () => {
    const f = render(); const c = f.componentInstance;
    c.inspect({ clientX: 390, currentTarget: { getBoundingClientRect: () => ({ left: 0, width: 800 }) } } as unknown as PointerEvent);
    expect(c.activeIndex).toBe(1); c.leave({ pointerType: 'mouse' } as PointerEvent); expect(c.activeIndex).toBeNull();
  });
  it('continues actuals at the same coordinates with an independently scaled forecast and accessible values',()=>{
    const f=render(points.map((p,i)=>({ ...p,forecastSpending:i===0 ? null : i===1 ? 250 : 1000,forecastContributions:i===0 ? null : i===1 ? 30 : 50 })));
    const c=f.componentInstance;
    expect(c.forecastPath.split(' L')[0]).toBe(`M ${c.plot[1].x} ${c.plot[1].actualY}`);
    expect(c.plot[2].forecastY!).toBeGreaterThanOrEqual(20); expect(c.plot[2].forecastY!).toBeLessThan(c.plot[2].plannedY);
    expect(f.nativeElement.querySelector('.forecast-line')).toBeTruthy(); expect(f.nativeElement.querySelector('.forecast-key').textContent).toBe('Forecast allocations');
    const slider=f.nativeElement.querySelector('[role="slider"]') as HTMLElement;
    slider.dispatchEvent(new KeyboardEvent('keydown',{ key:'End' })); f.detectChanges();
    expect(slider.getAttribute('aria-valuetext')).toContain('forecast allocations $1,050.00');
    expect(f.nativeElement.querySelector('.tooltip').textContent).toContain('Forecast allocations $1,050.00');
    c.selectScope('Contributions'); expect(c.plot[2].forecast).toBe(50);
    expect(c.forecastPath.split(' L')[0]).toBe(`M ${c.plot[1].x} ${c.plot[1].actualY}`);
  });
  it('hides a forecast with no continuation and never invents one from planned values',()=>{
    const f=render(); expect(f.nativeElement.querySelector('.forecast-line')).toBeNull();
    f.componentRef.setInput('points',points.map((p,i)=>({ ...p,forecastSpending:i===1 ? 250 : null,forecastContributions:i===1 ? 30 : null }))); f.detectChanges();
    expect(f.nativeElement.querySelector('.forecast-line')).toBeNull(); expect(f.nativeElement.querySelector('.forecast-summary')).toBeNull();
  });
  it('shows deficits below a visible zero line in the running remainder view',()=>{
    const f=render(points.map((p,i)=>({ ...p,plannedSpending:i===2 ? -300 : 100,plannedContributions:0,actualSpending:i===2 ? null : -100,actualContributions:i===2 ? null : 0,forecastSpending:i===0 ? null : -100-i*100,forecastContributions:i===0 ? null : 0 })));
    f.componentRef.setInput('remainderMode',true); f.detectChanges();
    const c=f.componentInstance; expect(c.ticks.some(t=>t.value<0)).toBe(true); expect(c.ticks.some(t=>t.value===0)).toBe(true);
    expect(f.nativeElement.querySelector('.zero-line')).toBeTruthy(); expect(f.nativeElement.querySelector('.scopes')).toBeNull();
    expect(c.plannedPath).not.toContain('NaN'); expect(c.forecastPath).not.toContain('NaN');
    const zero=c.ticks.find(t=>t.value===0)!.top*2.6; expect(c.plot[2].forecastY!).toBeGreaterThan(zero);
  });
});
