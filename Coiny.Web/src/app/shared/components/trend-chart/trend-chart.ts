import { Component, Input, OnChanges } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { TrendDataPoint } from '../../models/trends/trend-result';

interface PlotPoint extends TrendDataPoint { x: number; y: number; }

@Component({
  selector: 'app-trend-chart',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './trend-chart.html',
  styleUrl: './trend-chart.css',
})
export class TrendChart implements OnChanges {
  @Input() points: readonly TrendDataPoint[] = [];
  @Input() label = 'Balance history';

  plot: PlotPoint[] = [];
  linePath = '';
  areaPath = '';
  zeroY: number | null = null;
  activeIndex: number | null = null;
  private focused = false;

  get activePoint(): PlotPoint | null {
    return this.activeIndex === null ? null : this.plot[this.activeIndex] ?? null;
  }

  get tooltipLeft(): string {
    return `clamp(5.5rem, ${(this.activePoint?.x ?? 400) / 8}%, calc(100% - 5.5rem))`;
  }

  get accessibleValue(): string {
    const point = this.activePoint;
    if (!point) return 'Use arrow keys to explore daily balances.';
    const date = new Intl.DateTimeFormat('en-US', { dateStyle: 'long', timeZone: 'UTC' })
      .format(new Date(`${point.date}T00:00:00Z`));
    return `${date}: ${new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(point.value)}`;
  }

  ngOnChanges(): void {
    this.activeIndex = null;
    this.zeroY = null;
    if (!this.points.length) {
      this.plot = [];
      this.linePath = this.areaPath = '';
      return;
    }

    let min = Infinity;
    let max = -Infinity;
    for (const point of this.points) {
      min = Math.min(min, point.value);
      max = Math.max(max, point.value);
    }
    const padding = max === min ? Math.max(Math.abs(max) * 0.05, 1) : (max - min) * 0.12;
    const lower = min - padding;
    const upper = max + padding;
    const y = (value: number) => 64 + (upper - value) / (upper - lower) * 148;
    // Points use calendar dates rather than array positions, so sparse histories
    // also retain correct horizontal spacing.
    const firstDate = Date.parse(this.points[0].date);
    const lastDate = Date.parse(this.points[this.points.length - 1].date);
    this.plot = this.points.map(point => ({
      ...point,
      x: firstDate === lastDate ? 400 : 20 + (Date.parse(point.date) - firstDate) / (lastDate - firstDate) * 760,
      y: y(point.value),
    }));
    this.linePath = this.plot.map((point, index) => `${index ? 'L' : 'M'} ${point.x} ${point.y}`).join(' ');
    const first = this.plot[0];
    const last = this.plot[this.plot.length - 1];
    this.areaPath = `${this.linePath} L ${last.x} 232 L ${first.x} 232 Z`;
    if (min < 0 && max > 0) this.zeroY = y(0);
    if (this.focused) this.activeIndex = this.plot.length - 1;
  }

  inspect(event: PointerEvent): void {
    if (!this.plot.length) return;
    const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = (event.clientX - bounds.left) / Math.max(bounds.width, 1) * 800;
    let nearest = 0;
    for (let index = 1; index < this.plot.length; index++) {
      if (Math.abs(this.plot[index].x - x) < Math.abs(this.plot[nearest].x - x)) nearest = index;
    }
    this.activeIndex = nearest;
  }

  leave(event: PointerEvent): void {
    if (!this.focused && event.pointerType !== 'touch') this.activeIndex = null;
  }

  focus(): void {
    this.focused = true;
    if (this.plot.length && this.activeIndex === null) this.activeIndex = this.plot.length - 1;
  }

  blur(): void {
    this.focused = false;
    this.activeIndex = null;
  }

  onKeydown(event: KeyboardEvent): void {
    if (!this.plot.length) return;
    const index = this.activeIndex ?? this.plot.length - 1;
    switch (event.key) {
      case 'ArrowLeft': case 'ArrowDown': this.activeIndex = Math.max(0, index - 1); break;
      case 'ArrowRight': case 'ArrowUp': this.activeIndex = Math.min(this.plot.length - 1, index + 1); break;
      case 'Home': this.activeIndex = 0; break;
      case 'End': this.activeIndex = this.plot.length - 1; break;
      case 'Escape': this.activeIndex = null; break;
      default: return;
    }
    event.preventDefault();
  }
}
