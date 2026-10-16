import { afterNextRender, Component, ElementRef, inject, Input, OnDestroy, output, ViewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';

@Component({
  selector: 'app-modal',
  template: `<dialog #dialog aria-modal="true" [attr.aria-label]="title" (cancel)="cancel($event)" (click)="backdrop($event)">
    <header><h2>{{ title }}</h2><button type="button" aria-label="Close dialog" [disabled]="busy" (click)="dismiss.emit()">×</button></header>
    <div class="content"><ng-content /></div>
  </dialog>`,
  styleUrl: './modal.css',
})
export class Modal implements OnDestroy {
  @Input({ required: true }) title!: string;
  @Input() busy = false;
  readonly dismiss = output<void>();
  @ViewChild('dialog') dialog!: ElementRef<HTMLDialogElement>;
  private readonly document = inject(DOCUMENT);
  private previousFocus?: HTMLElement;
  private previousOverflow = '';
  constructor() {
    afterNextRender(() => {
      this.previousFocus = this.document.activeElement as HTMLElement;
      this.previousOverflow = this.document.body.style.overflow;
      this.document.body.style.overflow = 'hidden';
      this.dialog.nativeElement.showModal();
    });
  }
  cancel(event: Event): void { event.preventDefault(); if (!this.busy) this.dismiss.emit(); }
  backdrop(event: MouseEvent): void {
    if (event.target !== this.dialog.nativeElement || this.busy) return;
    const rect = this.dialog.nativeElement.getBoundingClientRect();
    if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)
      this.dismiss.emit();
  }
  ngOnDestroy(): void {
    this.dialog?.nativeElement.close();
    this.document.body.style.overflow = this.previousOverflow;
    if (this.previousFocus?.isConnected) this.previousFocus.focus();
  }
}
