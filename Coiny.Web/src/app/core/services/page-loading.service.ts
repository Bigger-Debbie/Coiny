import { DestroyRef, Injectable, inject, signal } from '@angular/core';

@Injectable({providedIn: 'root'})
export class PageLoadingService {
  readonly visible = signal(false);
  readonly fading = signal(false);
  private readonly pending = new Set<symbol>();
  private navigating = false;
  private deadlineReached = false;
  private startedAt = 0;
  private settleTimer?: ReturnType<typeof setTimeout>;
  private deadlineTimer?: ReturnType<typeof setTimeout>;
  private fadeTimer?: ReturnType<typeof setTimeout>;

  constructor() { inject(DestroyRef).onDestroy(() => this.cancel()); }
  begin(navigating = false) {
    this.clearTimers(); this.pending.clear(); this.startedAt = Date.now(); this.navigating = navigating; this.deadlineReached = false;
    this.fading.set(false); this.visible.set(true);
    // A failed/hung request must never leave the page permanently covered.
    this.deadlineTimer = setTimeout(() => { this.deadlineReached = true; this.fade(); }, 8000);
    this.schedule();
  }
  ready() { this.navigating = false; this.schedule(); }
  requestStarted() {
    if (!this.visible()) return undefined;
    const request = Symbol(); this.pending.add(request);
    clearTimeout(this.settleTimer);
    // A follow-up request during the fade belongs to the same initial load.
    if (this.fading() && !this.deadlineReached) { clearTimeout(this.fadeTimer); this.fading.set(false); }
    return request;
  }
  requestFinished(request: symbol | undefined) { if (request && this.pending.delete(request)) this.schedule(); }
  cancel() { this.clearTimers(); this.pending.clear(); this.visible.set(false); this.fading.set(false); this.navigating = false; }
  private schedule() {
    clearTimeout(this.settleTimer);
    if (!this.visible() || this.fading() || this.navigating || this.pending.size) return;
    // Let chained requests and the final render settle; keep quick loads visible for a brief moment.
    this.settleTimer = setTimeout(() => this.fade(), Math.max(150, 600 - (Date.now() - this.startedAt)));
  }
  private fade() {
    if (!this.visible() || this.fading()) return;
    clearTimeout(this.settleTimer);
    this.fading.set(true);
    this.fadeTimer = setTimeout(() => this.cancel(), 450);
  }
  private clearTimers() { clearTimeout(this.settleTimer); clearTimeout(this.deadlineTimer); clearTimeout(this.fadeTimer); }
}
