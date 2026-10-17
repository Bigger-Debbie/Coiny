import { afterNextRender, Component, DestroyRef, ElementRef, HostListener, inject, Injector, Input, output, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({ selector: 'app-review-cell', imports: [FormsModule, SelectMenu],
  template: `
    <button #trigger class="cell-value" [class.numeric]="type === 'number'" type="button" data-editable-cell [hidden]="editing()" [disabled]="disabled"
      [attr.aria-label]="label + ': ' + display" [attr.title]="display" (click)="begin()">{{ display }}</button>
    @if (editing()) {
      @if (type === 'select') {
        <app-select-menu [inputId]="id" [label]="label" [options]="options" [ngModel]="draft" (ngModelChange)="draft = $event; commit()" />
      } @else {
        <input #editor [id]="id" [type]="type" [min]="min" [step]="type === 'number' ? '0.01' : null" [attr.maxlength]="maxlength"
          [attr.aria-label]="label" [(ngModel)]="draft" (blur)="commit(false)" (keydown.enter)="$event.preventDefault(); commit()" />
      }
    }
  `,
  styleUrl: './review-cell.css',
})
export class ReviewCell {
  @Input() value: string | number | null = null;
  @Input() type: 'text' | 'date' | 'number' | 'select' = 'text';
  @Input() options: readonly SelectOption[] = [];
  @Input({ required: true }) label = '';
  @Input({ required: true }) id = '';
  @Input() min: string | number | null = null;
  @Input() maxlength: number | null = null;
  @Input() disabled = false;
  readonly valueChange = output<string | number | null>();
  readonly editing = signal(false);
  draft: string | number | null = null;
  @ViewChild('trigger') private trigger!: ElementRef<HTMLButtonElement>;
  @ViewChild('editor') private editor?: ElementRef<HTMLInputElement>;
  @ViewChild(SelectMenu) private menu?: SelectMenu;
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  get display(): string {
    if (this.type === 'select') return this.options.find(option => option.value === this.value)?.label ?? 'Choose…';
    if (this.value === null || this.value === '') return '—';
    return this.type === 'number' ? Number(this.value).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : String(this.value);
  }
  constructor() {
    const keyboard = (event: KeyboardEvent) => {
      if (!this.editing() || !['Escape', 'Tab'].includes(event.key)) return;
      event.preventDefault(); event.stopPropagation();
      if (event.key === 'Escape') this.cancel();
      else { this.commit(false); this.move(event.shiftKey ? -1 : 1); }
    };
    this.host.nativeElement.addEventListener('keydown', keyboard, true);
    inject(DestroyRef).onDestroy(() => this.host.nativeElement.removeEventListener('keydown', keyboard, true));
  }
  begin(): void {
    if (this.disabled || this.trigger.nativeElement.matches(':disabled')) return;
    this.draft = this.value; this.editing.set(true);
    this.afterRender(() => {
      if (!this.editing()) return;
      if (this.menu) { this.menu.trigger.nativeElement.focus(); this.menu.show(); }
      else {
        const input = this.editor?.nativeElement; input?.focus();
        if (this.type === 'date') { try { input?.showPicker?.(); } catch { /* Native picker may require a fresh gesture. */ } }
        else if (this.type === 'text') input?.select();
      }
    });
  }
  commit(restoreFocus = true): void {
    if (!this.editing()) return;
    const value = this.type === 'number' ? this.draft === '' || this.draft === null ? null : Number(this.draft) : this.draft;
    this.editing.set(false);
    if (value !== this.value) this.valueChange.emit(value);
    if (restoreFocus) this.afterRender(() => this.trigger.nativeElement.focus());
  }
  cancel(restoreFocus = true): void {
    this.editing.set(false); this.draft = this.value;
    if (restoreFocus) this.afterRender(() => this.trigger.nativeElement.focus());
  }
  @HostListener('document:pointerdown', ['$event']) outside(event: PointerEvent): void {
    if (this.editing() && this.type === 'select' && !this.host.nativeElement.contains(event.target as Node)) this.cancel(false);
  }
  private move(direction: number): void {
    this.afterRender(() => {
      const pane = this.host.nativeElement.closest('[data-review-surface]');
      const cells = Array.from(pane?.querySelectorAll<HTMLButtonElement>('[data-editable-cell]') ?? []).filter(button => !button.disabled && !button.matches(':disabled'));
      const next = cells[cells.indexOf(this.trigger.nativeElement) + direction];
      if (next) { next.focus(); next.click(); }
      else pane?.querySelector<HTMLElement>(direction < 0 ? '#review-search' : '[data-grid-next]')?.focus();
    });
  }
  private afterRender(callback: () => void): void { afterNextRender(callback, { injector: this.injector }); }
}
