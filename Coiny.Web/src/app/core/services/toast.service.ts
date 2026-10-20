import { DestroyRef, Injectable, inject, signal, WritableSignal } from '@angular/core';

export type ToastKind = 'success' | 'error';
export interface ToastMessage { id: number; message: string; kind: ToastKind; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  private sequence = 0;
  private readonly items = signal<ToastMessage[]>([]);
  readonly messages = this.items.asReadonly();
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  constructor() {
    inject(DestroyRef).onDestroy(() => { for (const timer of this.timers.values()) clearTimeout(timer); });
  }
  success(message: string) { this.show(message, 'success'); }
  error(message: string) { this.show(message, 'error'); }
  private show(message: string, kind: ToastKind) {
    if (!message.trim()) return;
    const id = ++this.sequence;
    this.items.update(items => [...items, { id, message, kind }]);
    this.timers.set(id, setTimeout(() => this.dismiss(id), 30_000));
  }
  dismiss(id: number) {
    clearTimeout(this.timers.get(id)); this.timers.delete(id);
    this.items.update(items => items.filter(item => item.id !== id));
  }
}

/** Retains local form state while announcing each nonempty action message globally. */
export function notificationSignal(kind: ToastKind): WritableSignal<string> {
  const toast = inject(ToastService);
  const state = signal('');
  const set = state.set.bind(state);
  state.set = message => { set(message); if (message.trim()) toast[kind](message); };
  state.update = update => state.set(update(state()));
  return state;
}
