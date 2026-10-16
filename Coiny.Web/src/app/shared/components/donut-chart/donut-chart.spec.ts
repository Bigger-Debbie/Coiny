import { TestBed } from '@angular/core/testing';
import { DonutChart } from './donut-chart';

const categories = [
  { categoryId: 1, categoryName: 'Groceries', amount: 75, percentage: 75, transactionCount: 10 },
  { categoryId: 2, categoryName: 'Restaurants', amount: 25, percentage: 25, transactionCount: 1 },
];
describe('DonutChart', () => {
  function render(data = categories) {
    const f = TestBed.createComponent(DonutChart);
    f.componentRef.setInput('categories', data); f.componentRef.setInput('total', 100); f.detectChanges(); return f;
  }
  it('renders proportional arcs, category amounts, counts, and accessible total', () => {
    const f = render();
    const slices = f.componentInstance.slices();
    expect(slices[0].percentage).toBe(75);
    expect(slices[1].percentage).toBe(25);
    expect(f.nativeElement.querySelectorAll('.slice')).toHaveLength(2);
    expect(f.nativeElement.querySelector('svg').getAttribute('aria-label')).toContain('$100.00');
    expect(f.nativeElement.textContent).toContain('10 transactions');
    expect(f.nativeElement.textContent).toContain('1 transaction');
  });
  it('highlights slices and legend rows together and exposes tooltip details', () => {
    const f = render();
    f.nativeElement.querySelector('.slice').dispatchEvent(new Event('pointerenter')); f.detectChanges();
    expect(f.nativeElement.querySelector('.legend .highlighted').textContent).toContain('Groceries');
    const tooltip = f.nativeElement.querySelector('[role="tooltip"]');
    expect(tooltip.textContent).toContain('$75.00');
    expect(tooltip.textContent).toContain('75.0%');
    expect(tooltip.textContent).toContain('10 transactions');
    f.nativeElement.querySelector('.slice').dispatchEvent(new Event('pointerleave'));
    f.nativeElement.querySelectorAll('.legend button')[1].dispatchEvent(new Event('pointerenter')); f.detectChanges();
    expect(f.nativeElement.querySelector('.slice.active').getAttribute('aria-label')).toContain('Restaurants');
  });
  it('supports focus, tap/click pinning, Escape, and resets stale selection on new data', () => {
    const f = render();
    const slice = f.nativeElement.querySelector('.slice');
    slice.dispatchEvent(new FocusEvent('focus')); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="tooltip"]')).toBeTruthy();
    slice.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    expect(f.componentInstance.pinned()).toBe(1);
    slice.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true })); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="tooltip"]')).toBeNull();
    slice.dispatchEvent(new MouseEvent('click')); f.detectChanges();
    expect(f.componentInstance.pinned()).toBe(1);
    f.componentRef.setInput('categories', [categories[1]]); f.detectChanges();
    expect(f.componentInstance.activeId()).toBeNull();
  });
  it('renders a complete single-category ring and keeps colors stable across reordering', () => {
    const f = render([categories[0]]);
    expect(f.componentInstance.slices()[0].dash).toBe('100 0');
    const color = f.componentInstance.slices()[0].color;
    f.componentRef.setInput('categories', [categories[1], categories[0]]); f.detectChanges();
    expect(f.componentInstance.slices().find(c => c.categoryId === 1)?.color).toBe(color);
  });
});
