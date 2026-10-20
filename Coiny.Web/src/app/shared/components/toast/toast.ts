import { Component, ElementRef, afterRenderEffect, inject, viewChild } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toasts',
  template: `<div #stack class="toast-stack" popover="manual" aria-label="Notifications">
    @for (toast of notifications.messages(); track toast.id) {
      <div class="toast" [class.error]="toast.kind === 'error'" [attr.role]="toast.kind === 'error' ? 'alert' : 'status'" aria-atomic="true">
        <span class="indicator" aria-hidden="true">{{ toast.kind === 'success' ? '✓' : '!' }}</span>
        <div><strong>{{ toast.kind === 'success' ? 'Success' : 'Error' }}</strong><p>{{ toast.message }}</p></div>
        <button type="button" (click)="notifications.dismiss(toast.id)" [attr.aria-label]="'Dismiss notification: ' + toast.message">×</button>
      </div>
    }
  </div>`,
  styleUrl: './toast.css',
})
export class Toasts {
  readonly notifications = inject(ToastService);
  private readonly stack = viewChild<ElementRef<HTMLElement>>('stack');
  constructor() {
    afterRenderEffect(() => {
      const element = this.stack()?.nativeElement;
      if (!element) return;
      // Popovers keep feedback visible above modal dialogs without trapping focus.
      if (this.notifications.messages().length) element.showPopover?.();
      else element.hidePopover?.();
    });
  }
}
