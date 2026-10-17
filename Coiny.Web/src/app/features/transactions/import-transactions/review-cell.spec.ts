import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ReviewCell } from './review-cell';

@Component({ selector: 'app-import-review', host: { 'data-review-surface': '' }, imports: [ReviewCell], template: `
  <input id="review-search" />
  <app-review-cell id="description" label="Description" [value]="description" (valueChange)="description = $any($event)" />
  <app-review-cell id="amount" label="Amount" type="number" [value]="amount" (valueChange)="amount = $any($event)" />
  <app-review-cell id="category" label="Category" type="select" [options]="options" [value]="category" (valueChange)="category = $any($event)" />
  <button data-grid-next>Check rows</button>
` })
class CellHost {
  description = 'Coffee'; amount = 5; category = 'a';
  options = [{ value: 'a', label: 'Dining' }, { value: 'b', label: 'Groceries' }];
}

describe('Spreadsheet cell editing', () => {
  async function render() {
    TestBed.configureTestingModule({ imports: [CellHost] });
    const f = TestBed.createComponent(CellHost); f.detectChanges(); await f.whenStable(); return f;
  }
  it('shows values until clicked, saves Enter, and cancels Escape', async () => {
    const f = await render();
    const cell = f.debugElement.queryAll(By.directive(ReviewCell))[0];
    expect(cell.nativeElement.querySelector('input')).toBeNull();
    cell.nativeElement.querySelector('button').click(); await f.whenStable();
    const input = cell.nativeElement.querySelector('input') as HTMLInputElement;
    expect(document.activeElement).toBe(input);
    input.value = 'Lunch'; input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await f.whenStable();
    expect(f.componentInstance.description).toBe('Lunch');
    cell.nativeElement.querySelector('button').click(); await f.whenStable();
    const next = cell.nativeElement.querySelector('input') as HTMLInputElement;
    next.value = 'Discard'; next.dispatchEvent(new Event('input', { bubbles: true }));
    next.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); await f.whenStable();
    expect(f.componentInstance.description).toBe('Lunch');
    expect(cell.nativeElement.querySelector('input')).toBeNull();
  });
  it('commits on Tab and opens the next editable cell, with Shift+Tab going back', async () => {
    const f = await render(); const cells = f.debugElement.queryAll(By.directive(ReviewCell));
    cells[0].nativeElement.querySelector('button').click(); await f.whenStable();
    const input = cells[0].nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'Updated'; input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true })); await f.whenStable();
    expect(f.componentInstance.description).toBe('Updated');
    const amount = cells[1].nativeElement.querySelector('input') as HTMLInputElement;
    expect(amount.type).toBe('number'); expect(document.activeElement).toBe(amount);
    amount.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true })); await f.whenStable();
    expect(cells[0].nativeElement.querySelector('input')).toBeTruthy();
  });
  it('opens the shared dropdown on click and commits a selection', async () => {
    const f = await render(); const cell = f.debugElement.queryAll(By.directive(ReviewCell))[2];
    cell.nativeElement.querySelector('button').click(); await f.whenStable();
    expect(cell.nativeElement.querySelector('[role="listbox"]')).toBeTruthy();
    cell.nativeElement.querySelectorAll('[role="option"]')[1].click(); await f.whenStable();
    expect(f.componentInstance.category).toBe('b');
    expect(cell.nativeElement.querySelector('app-select-menu')).toBeNull();
  });
});
