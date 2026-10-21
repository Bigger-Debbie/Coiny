import { Component, Input, OnChanges } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { BudgetComparisonPoint } from '../../shared/models/budget/budget-actuals';

type Scope = 'Spending' | 'Contributions' | 'All';
interface Point { date: string; planned: number; actual: number | null; x: number; plannedY: number; actualY: number | null; }

@Component({
  selector: 'app-budget-comparison-chart', imports: [CurrencyPipe, DatePipe],
  templateUrl: './budget-comparison-chart.html', styleUrl: './budget-comparison-chart.css',
})
export class BudgetComparisonChart implements OnChanges {
  @Input() points: readonly BudgetComparisonPoint[] = [];
  scope: Scope = 'All';
  plot: Point[] = [];
  ticks: { value: number; top: number }[] = [];
  plannedPath = ''; actualPath = '';
  activeIndex: number | null = null;
  private focused = false;
  private readonly money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
  private readonly compact = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumFractionDigits: 1 });
  get active() { return this.activeIndex === null ? null : this.plot[this.activeIndex] ?? null; }
  get middleDate() { return this.plot[Math.floor(this.plot.length / 2)]?.date; }
  get singleActual() { const actuals = this.plot.filter(p => p.actual !== null); return actuals.length === 1 ? actuals[0] : null; }
  get label() { return this.scope === 'All' ? 'All budget allocations' : this.scope === 'Contributions' ? 'Savings and investing' : 'Spending'; }
  get tooltipLeft() { return `clamp(6rem, ${(this.active?.x ?? 400) / 8}%, calc(100% - 6rem))`; }
  get accessibleValue() {
    const point = this.active;
    if (!point) return 'Use arrow keys to compare daily cumulative planned and actual amounts.';
    const date = new Intl.DateTimeFormat('en-US', { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(point.date + 'T00:00:00Z'));
    return `${date}: planned ${this.money.format(point.planned)}; ${point.actual === null ? 'no actuals yet' : 'actual ' + this.money.format(point.actual)}.`;
  }
  tick(value: number) { return this.compact.format(value); }
  ngOnChanges() { this.rebuild(); }
  selectScope(scope: Scope) { this.scope = scope; this.rebuild(); }
  private rebuild() {
    this.activeIndex = null;
    const values = this.points.map(p => ({ date: p.date,
      planned: this.scope === 'Spending' ? p.plannedSpending : this.scope === 'Contributions' ? p.plannedContributions : p.plannedSpending + p.plannedContributions,
      actual: p.actualSpending === null || p.actualContributions === null ? null : this.scope === 'Spending' ? p.actualSpending : this.scope === 'Contributions' ? p.actualContributions : p.actualSpending + p.actualContributions,
    }));
    const ceiling = Math.max(1, ...values.map(p => Math.max(p.planned, p.actual ?? 0))) * 1.08;
    const y = (value: number) => 20 + (ceiling - value) / ceiling * 220;
    this.ticks = [0, .25, .5, .75, 1].map(fraction => ({ value: ceiling * fraction, top: y(ceiling * fraction) / 260 * 100 }));
    this.plot = values.map((p, index) => ({ ...p, x: values.length < 2 ? 400 : 16 + index / (values.length - 1) * 768,
      plannedY: y(p.planned), actualY: p.actual === null ? null : y(p.actual) }));
    this.plannedPath = this.plot.map((p, i) => `${i ? 'L' : 'M'} ${p.x} ${p.plannedY}`).join(' ');
    this.actualPath = this.plot.filter(p => p.actualY !== null).map((p, i) => `${i ? 'L' : 'M'} ${p.x} ${p.actualY}`).join(' ');
    if (this.focused) this.focus();
  }
  inspect(event: PointerEvent) {
    if (!this.plot.length) return;
    const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = (event.clientX - bounds.left) / Math.max(1, bounds.width) * 800;
    this.activeIndex = this.plot.reduce((best, p, i) => Math.abs(p.x - x) < Math.abs(this.plot[best].x - x) ? i : best, 0);
  }
  leave(event: PointerEvent) { if (!this.focused && event.pointerType !== 'touch') this.activeIndex = null; }
  focus() { this.focused = true; if (this.activeIndex === null && this.plot.length) { const actuals = this.plot.filter(p => p.actual !== null); this.activeIndex = actuals.length ? this.plot.indexOf(actuals[actuals.length - 1]) : this.plot.length - 1; } }
  blur() { this.focused = false; this.activeIndex = null; }
  keydown(event: KeyboardEvent) {
    if (!this.plot.length) return;
    const index = this.activeIndex ?? 0;
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
