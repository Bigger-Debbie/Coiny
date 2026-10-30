import { Modal } from '../../../shared/components/modal/modal';
import { Component, DestroyRef, inject, Input, OnChanges, SimpleChanges, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { BudgetService } from '../../../core/services/budget.service';
import { AvailableCashOutlook } from '../../../shared/models/budget/budget-actuals';
@Component({ selector:'app-available-cash', imports:[Modal,CurrencyPipe,DatePipe], templateUrl:'./available-cash.html', styleUrl:'./available-cash.css' })
export class AvailableCash implements OnChanges {
  @Input({required:true}) asOf = '';
  @Input() refreshKey = 0;
  @Input() accountIds: number[] | null = null;
  readonly accountsSaved = output<number[]>();
  readonly amountChanged = output<number|null>();
  private readonly api = inject(BudgetService);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  readonly data = signal<AvailableCashOutlook|null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly detailsOpen = signal(false);
  openDetails() { this.draft = [...(this.data()?.accountIds ?? [])]; this.detailsOpen.set(true); }
  draft: number[] = [];
  ngOnChanges(changes: SimpleChanges) {
    const current = this.data();
    if (changes['asOf'] || changes['refreshKey'] || !current || this.accountIds?.slice().sort().join(',') !== current.accountIds.slice().sort().join(',')) this.load(this.accountIds);
  }
  load(ids: number[] | null = this.accountIds, save = false) {
    this.request?.unsubscribe(); this.loading.set(true); this.error.set(''); this.amountChanged.emit(null);
    this.request = this.api.availableCash(this.asOf,ids).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: data => {
        this.data.set(data); this.draft = [...data.accountIds]; this.loading.set(false); this.amountChanged.emit(data.expectedRemaining);
        if (save) { this.accountsSaved.emit(data.accountIds); this.detailsOpen.set(false); }
      },
      error: error => { this.loading.set(false); this.error.set(error.error?.detail ?? 'Available cash couldn’t load. Please try again.'); },
    });
  }
  toggle(id: number, checked: boolean) { this.draft = checked ? [...this.draft.filter(i => i !== id),id] : this.draft.filter(i => i !== id); }
  get hasCash() { return this.data()?.accounts.some(a => this.draft.includes(a.id) && a.accountType !== 'CreditCard') ?? false; }
  get changed() { return this.draft.slice().sort().join(',') !== this.data()?.accountIds.slice().sort().join(','); }
  apply() { if (this.hasCash) this.load(this.draft,true); }
}
