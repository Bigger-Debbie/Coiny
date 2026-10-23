import { Component, computed, Input, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { BreakdownType, CategoryBreakdownItem } from '../../models/transactions/category-breakdown';

let nextChartId = 0;
const colors = ['#2f305f', '#eb9a50', '#047c04', '#167b83', '#7b4a96', '#d4a027', '#9b001e', '#486c9b'];
// Explicit colors take priority. Allocate the rest by ID so input order cannot
// change a category's color or let an automatic slice reuse a reserved color.
const normalizeColor = (color: string) => {
  const value = color.toLowerCase().replace(/\s+/g, '');
  return /^#[0-9a-f]{3}$/.test(value) ? '#' + [...value.slice(1)].map(c => c + c).join('') : value;
};
function sliceColors(categories: readonly CategoryBreakdownItem[]) {
  const sorted = [...categories].sort((a,b) => a.categoryId - b.categoryId);
  const assigned = new Map<number,string>();
  const reserved = new Set(sorted.filter(c => c.color).map(c => normalizeColor(c.color!)));
  const explicit = new Set<string>();
  for (const category of sorted) {
    if (category.color && !explicit.has(normalizeColor(category.color))) {
      assigned.set(category.categoryId,category.color); explicit.add(normalizeColor(category.color));
    }
  }
  for (const category of sorted) {
    if (assigned.has(category.categoryId)) continue;
    const preferred = colors[((category.categoryId - 1) % colors.length + colors.length) % colors.length];
    let color = !reserved.has(normalizeColor(preferred)) ? preferred : colors.find(c => !reserved.has(normalizeColor(c)));
    for (let attempt = 0; !color; attempt++) {
      const candidate = `hsl(${((category.categoryId * 137.508 + attempt * 47.13) % 360 + 360) % 360} 65% 38%)`;
      if (!reserved.has(normalizeColor(candidate))) color = candidate;
    }
    assigned.set(category.categoryId,color); reserved.add(normalizeColor(color));
  }
  return assigned;
}
@Component({
  selector: 'app-donut-chart',
  host: { '[class.scroll-legend]': 'scrollLegend' },
  imports: [CurrencyPipe, DecimalPipe],
  templateUrl: './donut-chart.html',
  styleUrl: './donut-chart.css',
})
export class DonutChart {
  private readonly data = signal<readonly CategoryBreakdownItem[]>([]);
  @Input() set categories(value: readonly CategoryBreakdownItem[]) {
    this.data.set(value);
    this.hovered.set(null); this.focused.set(null); this.pinned.set(null);
  }
  @Input() scrollLegend = false;
  @Input() total = 0;
  @Input() centerLabel = '';
  @Input() periodLabel = 'Selected period';
  @Input() chartLabel = '';
  @Input() showCounts = true;
  @Input() unsigned = false;
  @Input() type: BreakdownType = 'Expense';
  readonly tooltipId = 'donut-tooltip-' + nextChartId++;
  readonly hovered = signal<number | null>(null);
  readonly focused = signal<number | null>(null);
  readonly pinned = signal<number | null>(null);
  readonly activeId = computed(() => this.hovered() ?? this.focused() ?? this.pinned());
  readonly slices = computed(() => {
    const categories = this.data().filter(c => c.amount > 0);
    const assignedColors = sliceColors(categories);
    const total = categories.reduce((sum, c) => sum + c.amount, 0);
    let offset = 0;
    return categories.map(category => {
      const percentage = total ? category.amount / total * 100 : 0;
      const gap = categories.length > 1 ? Math.min(.65, percentage * .15) : 0;
      const angle = (offset + percentage / 2) / 100 * Math.PI * 2 - Math.PI / 2;
      const slice = {
        ...category, percentage,
        dash: (percentage - gap) + ' ' + (100 - percentage + gap),
        offset: -(offset + gap / 2),
        strokeWidth: category.strokeWidth ?? 38,
        color: assignedColors.get(category.categoryId)!,
        initial: Array.from(category.categoryName.trim())[0]?.toUpperCase() || '?',
        dx: Math.cos(angle) * 4, dy: Math.sin(angle) * 4,
        tooltipX: 50 + Math.cos(angle) * 22,
        tooltipY: 50 + Math.sin(angle) * 24,
      };
      offset += percentage;
      return slice;
    });
  });
  readonly active = computed(() => this.slices().find(c => c.categoryId === this.activeId()));
  toggle(id: number): void { this.pinned.update(value => value === id ? null : id); }
  dismiss(): void { this.hovered.set(null); this.focused.set(null); this.pinned.set(null); }
  onKey(event: KeyboardEvent, id: number): void {
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); this.toggle(id); }
    if (event.key === 'Escape') { event.preventDefault(); this.dismiss(); }
  }
  description(category: CategoryBreakdownItem): string {
    return category.categoryName + ': ' + new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
      .format(category.amount) + ', ' + category.percentage.toFixed(1) + '%'
      + (this.showCounts ? ', ' + category.transactionCount + (category.transactionCount === 1 ? ' transaction' : ' transactions') : '');
  }
}
