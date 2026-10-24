import { Component, computed, DestroyRef, inject, Input, OnChanges, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { BudgetService } from '../../core/services/budget.service';
import { BudgetForecast, BudgetComparisonPoint } from '../../shared/models/budget/budget-actuals';
import { BudgetComparisonChart } from './budget-comparison-chart';

@Component({
  selector:'app-budget-running-forecast',imports:[CurrencyPipe,DatePipe,BudgetComparisonChart],
  templateUrl:'./budget-running-forecast.html',styleUrl:'./budget-running-forecast.css',
})
export class BudgetRunningForecast implements OnChanges {
  @Input({ required:true }) asOf=''; @Input() refreshKey=0;
  private readonly api=inject(BudgetService); private readonly destroy=inject(DestroyRef); private request?: Subscription;
  readonly months=signal(3); readonly data=signal<BudgetForecast | null>(null); readonly loading=signal(false); readonly error=signal('');
  readonly ranges=[{months:0,label:'This period'},{months:1,label:'1 month'},{months:3,label:'3 months'},{months:6,label:'6 months'},{months:12,label:'1 year'}];
  readonly points=computed<BudgetComparisonPoint[]>(()=>this.data()?.points.map(p=>({ date:p.date,plannedSpending:p.planned,plannedContributions:0,
    actualSpending:p.actual,actualContributions:p.actual===null ? null : 0,forecastSpending:p.forecast,forecastContributions:p.forecast===null ? null : 0 })) ?? []);
  get end() { return this.data()?.points.at(-1); }
  get forecastRemainder() { return this.end?.forecast ?? this.end?.actual ?? 0; }
  get completed() { const d=this.data(); return !!d?.throughDate && d.throughDate>=d.endDate; }
  ngOnChanges() { this.load(); }
  select(months:number) { if (months===this.months()) return; this.months.set(months); this.load(); }
  load() {
    if (!this.asOf) return;
    this.request?.unsubscribe(); this.loading.set(true); this.error.set(''); this.data.set(null);
    this.request=this.api.forecast(this.asOf,this.months()).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next:data=>{ this.data.set(data); this.loading.set(false); },
      error:e=>{ this.error.set(e.error?.detail || 'The running forecast couldn’t load. Try again.'); this.loading.set(false); },
    });
  }
}
