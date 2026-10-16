import { Component, ElementRef, forwardRef, HostListener, Input, signal, ViewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption { value: string | number | null; label: string; detail?: string; }

@Component({
  selector: 'app-select-menu',
  templateUrl: './select-menu.html',
  styleUrl: './select-menu.css',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SelectMenu), multi: true }],
})
export class SelectMenu implements ControlValueAccessor {
  @Input({ required: true }) inputId!: string;
  @Input({ required: true }) label!: string;
  @Input() options: readonly SelectOption[] = [];
  @Input() inverse = false;
  @ViewChild('trigger') trigger!: ElementRef<HTMLButtonElement>;
  readonly value = signal<string | number | null>(null);
  readonly disabled = signal(false);
  readonly open = signal(false);
  readonly active = signal(0);
  readonly position = signal({ left: 0, top: 0, width: 0, height: 280, above: false });
  private onChange: (value: string | number | null) => void = () => {};
  private onTouched = () => {};
  private prefix = '';
  private lastTyped = 0;
  constructor(private readonly host: ElementRef<HTMLElement>) {}
  get selected(): SelectOption | undefined { return this.options.find(o => o.value === this.value()); }
  writeValue(value: string | number | null): void { this.value.set(value); }
  registerOnChange(fn: typeof this.onChange): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(value: boolean): void { this.disabled.set(value); if (value) this.close(); }

  toggle(): void { if (this.open()) this.close(); else this.show(); }
  show(): void {
    if (this.disabled() || this.trigger.nativeElement.matches(':disabled')) return;
    const rect = this.trigger.nativeElement.getBoundingClientRect();
    const below = window.innerHeight - rect.bottom - 12;
    const above = rect.top - 12;
    const placeAbove = below < Math.min(280, this.options.length * 54 + 12) && above > below;
    const height = Math.max(40, Math.min(280, placeAbove ? above : below));
    const width = Math.min(Math.max(rect.width, 230), window.innerWidth - 24);
    this.position.set({
      left: Math.max(12, Math.min(rect.left, window.innerWidth - width - 12)),
      top: placeAbove ? rect.top - 6 : rect.bottom + 6,
      width, height, above: placeAbove,
    });
    this.active.set(Math.max(0, this.options.findIndex(o => o.value === this.value())));
    this.open.set(true);
    this.scrollActive();
  }
  close(): void { if (this.open()) { this.open.set(false); this.onTouched(); } }
  choose(option: SelectOption): void {
    if (this.disabled() || this.trigger.nativeElement.matches(':disabled')) return;
    this.value.set(option.value);
    this.onChange(option.value);
    this.close();
    this.trigger.nativeElement.focus();
  }
  onKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key === 'Tab') { this.close(); return; }
    if (key === 'Escape') {
      if (this.open()) { event.preventDefault(); event.stopPropagation(); this.close(); }
      return;
    }
    if (['ArrowDown', 'ArrowUp', 'Home', 'End', 'Enter', ' '].includes(key)) {
      event.preventDefault();
      if (!this.open()) { this.show(); return; }
      if (key === 'Enter' || key === ' ') { const option = this.options[this.active()]; if (option) this.choose(option); return; }
      const last = this.options.length - 1;
      this.active.set(key === 'Home' ? 0 : key === 'End' ? last :
        Math.max(0, Math.min(last, this.active() + (key === 'ArrowDown' ? 1 : -1))));
      this.scrollActive();
    } else if (key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      if (!this.open()) this.show();
      this.prefix = Date.now() - this.lastTyped < 700 ? this.prefix + key.toLowerCase() : key.toLowerCase();
      this.lastTyped = Date.now();
      const match = this.options.findIndex(o => o.label.toLowerCase().startsWith(this.prefix));
      if (match >= 0) { this.active.set(match); this.scrollActive(); }
    }
  }
  private scrollActive(): void {
    requestAnimationFrame(() => this.host.nativeElement.querySelector('.highlighted')?.scrollIntoView?.({ block: 'nearest' }));
  }
  @HostListener('document:click', ['$event'])
  outside(event: MouseEvent): void { if (!this.host.nativeElement.contains(event.target as Node)) this.close(); }
  @HostListener('focusout', ['$event'])
  blur(event: FocusEvent): void {
    if (!this.host.nativeElement.contains(event.relatedTarget as Node | null)) { this.close(); this.onTouched(); }
  }
  @HostListener('window:resize')
  @HostListener('window:scroll')
  viewportChanged(): void { if (this.open()) this.close(); }
}
