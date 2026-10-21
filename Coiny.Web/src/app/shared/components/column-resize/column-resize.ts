import { Component, Input, output } from '@angular/core';

@Component({
  selector: 'app-column-resize',
  template: `<button type="button" role="separator" aria-orientation="vertical"
    [attr.aria-label]="'Resize ' + label + ' column'" [attr.aria-valuemin]="min" [attr.aria-valuemax]="max" [attr.aria-valuenow]="width" [attr.aria-valuetext]="width + ' pixels'"
    title="Drag to resize. Arrow keys adjust width; double-click to reset."
    (pointerdown)="start($event)" (pointermove)="move($event)" (pointerup)="finish($event)" (pointercancel)="finish($event)" (lostpointercapture)="pointerId = null"
    (keydown)="key($event)" (dblclick)="resized.emit(defaultWidth)" (click)="$event.stopPropagation()"></button>`,
  styles: `:host { position:absolute; right:-5px; top:0; bottom:0; width:12px; z-index:5; }
    button { display:block; width:100%; height:100%; padding:0; margin:0; border:0; border-radius:0; background:transparent; cursor:col-resize; touch-action:none; }
    button::after { content:''; position:absolute; top:15%; bottom:15%; left:5px; width:2px; background:transparent; }
    button:hover, button:focus-visible { transform:none; background:transparent; outline:none; }
    button:hover::after,button:focus-visible::after { background:var(--color-accent); }
    button:focus-visible::after { width:3px; }`,
})
export class ColumnResize {
  @Input({required:true}) label = '';
  @Input({required:true}) width = 100;
  @Input() min = 64;
  @Input() max = 640;
  @Input() defaultWidth = 100;
  readonly resized = output<number>();
  pointerId: number | null = null;
  private startX = 0;
  private startWidth = 0;
  start(event: PointerEvent) {
    if (!event.isPrimary || event.button !== 0) return;
    event.preventDefault(); event.stopPropagation();
    this.pointerId = event.pointerId; this.startX = event.clientX; this.startWidth = this.width;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }
  move(event: PointerEvent) {
    if (event.pointerId !== this.pointerId) return;
    event.preventDefault(); this.emit(this.startWidth + event.clientX - this.startX);
  }
  finish(event: PointerEvent) {
    if (event.pointerId !== this.pointerId) return;
    this.pointerId = null;
    const target = event.currentTarget as HTMLElement;
    if (target.hasPointerCapture(event.pointerId)) target.releasePointerCapture(event.pointerId);
  }
  key(event: KeyboardEvent) {
    const step = event.shiftKey ? 64 : 16;
    const value = event.key === 'ArrowLeft' ? this.width-step : event.key === 'ArrowRight' ? this.width+step : event.key === 'Home' ? this.min : event.key === 'End' ? this.max : null;
    if (value === null) return;
    event.preventDefault(); event.stopPropagation(); this.emit(value);
  }
  private emit(value:number) { this.resized.emit(Math.round(Math.max(this.min,Math.min(this.max,value)))); }
}
