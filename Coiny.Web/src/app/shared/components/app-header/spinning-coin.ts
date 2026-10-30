import { Directive, ElementRef, HostListener, NgZone, OnDestroy, inject } from '@angular/core';

/** The wrapper keeps its hit area steady while its coin turns in 3D. */
@Directive({ selector: '[appSpinningCoin]' })
export class SpinningCoin implements OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly zone = inject(NgZone);
  private frame = 0;
  private x = 0;
  private y = 0;
  private entry: { x: number; y: number } | null = null;
  private triggered = false;

  @HostListener('pointerenter', ['$event']) enter(event: PointerEvent) {
    this.entry = event.pointerType === 'mouse' ? { x: event.clientX, y: event.clientY } : null;
    this.triggered = false;
  }
  @HostListener('pointermove', ['$event']) move(event: PointerEvent) {
    if (!this.entry || this.triggered || event.pointerType !== 'mouse') return;
    const dx = event.clientX - this.entry.x;
    const dy = event.clientY - this.entry.y;
    if (Math.max(Math.abs(dx), Math.abs(dy)) < 2) return;
    this.triggered = true;
    this.spin(dx, dy);
  }
  @HostListener('pointerleave') leave() { this.entry = null; this.triggered = false; }

  private spin(dx: number, dy: number) {
    const motion = window.matchMedia?.('(prefers-reduced-motion: reduce)');
    if (motion?.matches) return;
    const coin = this.host.nativeElement.querySelector('img');
    if (!coin) return;
    cancelAnimationFrame(this.frame);
    const horizontal = Math.abs(dx) >= Math.abs(dy);
    const startX = this.x, startY = this.y;
    // Finish every pass on a whole turn so the original artwork faces forward.
    const targetX = horizontal ? Math.round(startX / 360) * 360 : (dy > 0 ? Math.floor(startX / 360) * 360 - 1080 : Math.ceil(startX / 360) * 360 + 1080);
    const targetY = !horizontal ? Math.round(startY / 360) * 360 : (dx > 0 ? Math.ceil(startY / 360) * 360 + 1080 : Math.floor(startY / 360) * 360 - 1080);
    this.zone.runOutsideAngular(() => {
      const start = performance.now();
      const tick = (now: number) => {
        const progress = Math.min(1, Math.max(0, (now - start) / 1800));
        const eased = 1 - Math.pow(1 - progress, 3);
        this.x = startX + (targetX - startX) * eased;
        this.y = startY + (targetY - startY) * eased;
        if (progress === 1 || motion?.matches) {
          this.x = this.y = this.frame = 0; coin.style.transform = ''; coin.style.willChange = ''; return;
        }
        coin.style.willChange = 'transform';
        coin.style.transform = `rotateX(${this.x}deg) rotateY(${this.y}deg)`;
        this.frame = requestAnimationFrame(tick);
      };
      this.frame = requestAnimationFrame(tick);
    });
  }
  ngOnDestroy() { cancelAnimationFrame(this.frame); }
}
