import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { SelectMenu } from './select-menu';

@Component({
  imports: [FormsModule, SelectMenu],
  template: '<app-select-menu inputId="test-select" label="Account" [options]="options" [(ngModel)]="value" [disabled]="disabled()" />',
})
class Host {
  value: number | null = 1;
  disabled = signal(false);
  options = [{ value: 1, label: 'Checking' }, { value: 2, label: 'Savings' }, { value: null, label: 'No institution' }];
}
describe('SelectMenu', () => {
  async function render() {
    const f = TestBed.createComponent(Host); f.detectChanges(); await f.whenStable(); return f;
  }
  it('uses the custom listbox and writes numeric and null values through ngModel', async () => {
    const f = await render();
    const trigger = f.nativeElement.querySelector('button');
    trigger.click(); f.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    f.nativeElement.querySelectorAll('[role="option"]')[1].click();
    await f.whenStable();
    expect(f.componentInstance.value).toBe(2);
    expect(trigger.textContent).toContain('Savings');
    trigger.click(); f.detectChanges();
    f.nativeElement.querySelectorAll('[role="option"]')[2].click();
    await f.whenStable();
    expect(f.componentInstance.value).toBeNull();
  });
  it('supports arrow navigation, Enter, Escape, and typeahead', async () => {
    const f = await render();
    const trigger = f.nativeElement.querySelector('button');
    const key = (value: string) => {
      trigger.dispatchEvent(new KeyboardEvent('keydown', { key: value, bubbles: true, cancelable: true }));
      f.detectChanges();
    };
    key('ArrowDown'); key('ArrowDown'); key('Enter'); await f.whenStable();
    expect(f.componentInstance.value).toBe(2);
    key('ArrowDown'); key('Escape');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    key('c'); key('Enter'); await f.whenStable();
    expect(f.componentInstance.value).toBe(1);
  });
  it('dismisses on outside click and respects disabled state', async () => {
    const f = await render();
    const trigger = f.nativeElement.querySelector('button');
    trigger.click(); f.detectChanges();
    document.body.click(); f.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    f.componentInstance.disabled.set(true); f.detectChanges(); await f.whenStable();
    trigger.click(); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="listbox"]')).toBeNull();
  });
});
