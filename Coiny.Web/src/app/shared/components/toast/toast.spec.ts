import { TestBed } from '@angular/core/testing';
import { Toasts } from './toast';
import { ToastService } from '../../../core/services/toast.service';

describe('Toasts', () => {
  it('announces success and errors, escapes message text, and offers dismissal', () => {
    const f = TestBed.createComponent(Toasts); const service = TestBed.inject(ToastService);
    service.success('Account created.'); service.error('<b>Failed</b>'); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="status"]').textContent).toContain('Account created.');
    expect(f.nativeElement.querySelector('[role="alert"]').textContent).toContain('<b>Failed</b>');
    expect(f.nativeElement.querySelector('b')).toBeNull();
    f.nativeElement.querySelector('button').click(); f.detectChanges();
    expect(service.messages()).toHaveLength(1); expect(service.messages()[0].kind).toBe('error');
  });
});
