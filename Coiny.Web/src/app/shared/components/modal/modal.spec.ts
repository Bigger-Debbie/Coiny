import { TestBed } from '@angular/core/testing';
import { Modal } from './modal';

describe('Modal', () => {
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function(this: HTMLDialogElement) { this.setAttribute('open', ''); } });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function(this: HTMLDialogElement) { this.removeAttribute('open'); } });
  });
  afterAll(() => {
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal');
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'close');
  });
  it('opens as a modal, locks scroll, and restores focus on dismissal', async () => {
    const trigger = document.createElement('button'); document.body.append(trigger); trigger.focus();
    const f = TestBed.createComponent(Modal); f.componentRef.setInput('title', 'Add Account');
    f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('dialog').hasAttribute('open')).toBe(true);
    expect(document.body.style.overflow).toBe('hidden');
    f.destroy();
    expect(document.body.style.overflow).toBe('');
    expect(document.activeElement).toBe(trigger);
    trigger.remove();
  });
  it('blocks dismissal during save and handles Escape when idle', async () => {
    const f = TestBed.createComponent(Modal); f.componentRef.setInput('title', 'Add Transaction');
    const dismiss = vi.fn(); f.componentInstance.dismiss.subscribe(dismiss);
    f.componentRef.setInput('busy', true); f.detectChanges(); await f.whenStable();
    const dialog = f.nativeElement.querySelector('dialog');
    const cancel = new Event('cancel', { cancelable: true });
    dialog.dispatchEvent(cancel);
    expect(cancel.defaultPrevented).toBe(true);
    expect(dismiss).not.toHaveBeenCalled();
    f.componentRef.setInput('busy', false); f.detectChanges();
    dialog.dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(dismiss).toHaveBeenCalledOnce();
    f.destroy();
  });
});
