import { Component, DestroyRef, inject, Input, OnChanges, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { TransactionService } from '../../../core/services/transaction.service';
import { BreakdownType, CategoryBreakdown } from '../../../shared/models/transactions/category-breakdown';
import { DonutChart } from '../../../shared/components/donut-chart/donut-chart';

@Component({
  selector: 'app-category-breakdown',
  imports: [DatePipe, DonutChart],
  templateUrl: './category-breakdown.html',
  styleUrl: './category-breakdown.css',
})
export class CategoryBreakdownPanel implements OnChanges {
  @Input() accountId: number | null = null;
  @Input({ required: true }) startDate = '';
  @Input({ required: true }) endDate = '';
  @Input() refreshKey = 0;
  private readonly service = inject(TransactionService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly type = signal<BreakdownType>('Expense');
  readonly breakdown = signal<CategoryBreakdown | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);
  ngOnChanges(): void { if (this.startDate && this.endDate) this.load(); }
  selectType(type: BreakdownType): void {
    if (this.type() === type) return;
    this.type.set(type);
    this.load();
  }
  load(): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set(false);
    this.breakdown.set(null);
    this.request = this.service.getCategoryBreakdown({
      accountId: this.accountId ?? undefined, startDate: this.startDate, endDate: this.endDate, categoryType: this.type(),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => { this.breakdown.set(result); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }
}
