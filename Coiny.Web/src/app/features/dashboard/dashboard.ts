import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { HouseholdService } from '../../core/services/household.service';
import { DashboardSummary } from '../../shared/models/dashboard/dashboard-summary';
import { TrendResult } from '../../shared/models/trends/trend-result';
import { TrendChart } from '../../shared/components/trend-chart/trend-chart';
import { Icon } from '../../shared/components/icon/icon';
import { getTrendDates, TrendRange } from './trend-range';
import { AccountList } from './account-list/account-list';
import { TransactionList } from './transaction-list/transaction-list';

@Component({
  selector: 'app-dashboard',
  imports: [CurrencyPipe, DecimalPipe, TrendChart, Icon, AccountList, TransactionList],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly authService = inject(AuthService);
  private readonly householdService = inject(HouseholdService);
  private readonly destroyRef = inject(DestroyRef);
  private trendRequest?: Subscription;

  readonly dashboard = signal<DashboardSummary | null>(null);
  readonly summaryLoading = signal(true);
  readonly summaryError = signal(false);
  readonly trend = signal<TrendResult | null>(null);
  readonly trendLoading = signal(true);
  readonly trendError = signal(false);
  readonly selectedRange = signal<TrendRange>('1M');
  readonly ranges: TrendRange[] = ['1D', '1W', '1M', '3M', '6M', '1Y', 'YTD'];
  readonly rangeDescriptions: Record<TrendRange, string> = {
    '1D': 'the past day',
    '1W': 'the past week',
    '1M': 'the past month', '3M': 'the past 3 months', '6M': 'the past 6 months',
    YTD: 'this year', '1Y': 'the past year',
  };
  readonly absoluteChange = computed(() => Math.abs(this.trend()?.absoluteChange ?? 0));
  readonly percentageMagnitude = computed(() => Math.abs(this.trend()?.percentageChange ?? 0));
  readonly firstName = signal<string | null>(null);
  readonly householdName = signal<string | null>(null);
  readonly householdGreeting = computed(() => {
    const name = this.householdName()?.trim();
    if (!name) return 'Your household at a glance.';
    const title = /^the\s/i.test(name) ? name : `The ${name}`;
    return `${title}${/household$/i.test(name) ? '' : ' household'} at a glance.`;
  });

  ngOnInit(): void {
    this.loadSummary();
    this.loadTrend();
    this.authService.getCurrentUser().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: user => this.firstName.set(user.firstName?.trim() || null),
      error: () => this.firstName.set(null),
    });
    this.householdService.getCurrentHousehold().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: household => this.householdName.set(household.name),
      error: () => this.householdName.set(null),
    });
  }

  loadSummary(): void {
    this.summaryLoading.set(true);
    this.summaryError.set(false);
    this.dashboardService.getSummary().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: summary => { this.dashboard.set(summary); this.summaryLoading.set(false); },
      error: () => { this.summaryError.set(true); this.summaryLoading.set(false); },
    });
  }

  selectRange(range: TrendRange): void {
    if (range === this.selectedRange()) return;
    this.selectedRange.set(range);
    this.loadTrend();
  }

  loadTrend(): void {
    // Cancel a superseded request so a slow response cannot replace the latest range.
    this.trendRequest?.unsubscribe();
    this.trendLoading.set(true);
    this.trendError.set(false);
    this.trend.set(null);
    const dates = getTrendDates(this.selectedRange());
    this.trendRequest = this.dashboardService.getNetWorthTrend(dates.startDate, dates.endDate)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => { this.trend.set(result); this.trendLoading.set(false); },
        error: () => { this.trendError.set(true); this.trendLoading.set(false); },
      });
  }

}
