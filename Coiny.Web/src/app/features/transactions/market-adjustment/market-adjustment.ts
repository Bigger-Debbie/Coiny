import { Component, DestroyRef, Input, OnChanges, inject, output, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { TransactionService } from '../../../core/services/transaction.service';

@Component({
  selector: 'app-market-adjustment', imports: [CurrencyPipe],
  template: `
    <section aria-label="Market adjustment preview" aria-live="polite" [attr.aria-busy]="loading()">
      @if (loading()) { <p>Checking the account balance…</p> }
      @else if (error()) { <p class="error">{{ error() }}</p><button type="button" class="button-ghost" (click)="load()">Try again</button> }
      @else if (baseline() !== null) {
        <p>Coiny balance on this date: <strong>{{ baseline() | currency }}</strong></p>
        @if (validBalance) {
          <p><strong>{{ difference > 0 ? 'Market gain' : difference < 0 ? 'Market loss' : 'No change' }}: {{ magnitude(difference) | currency }}</strong></p>
        }
      }
      <p class="hint">Updates the account value without counting as income, spending, or a contribution. Include any deposits and withdrawals for this date first.</p>
    </section>`,
  styles: `section { background: var(--color-surface-alt, #f7f7fb); border-radius: 8px; padding: 12px 16px; } p { margin: 0; } p + p { margin-top: 10px; } .hint { font-size: .875rem; color: var(--color-text-muted, #5f6075); } .error { color: #ac0029; }`,
})
export class MarketAdjustment implements OnChanges {
  @Input() accountId = 0;
  @Input() date = '';
  @Input() balance: number | null = null;
  @Input() excludingId?: number;
  @Input() refresh = 0;
  readonly checked = output<number | null>();
  readonly baseline = signal<number | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly magnitude = Math.abs;
  private readonly api = inject(TransactionService);
  private readonly destroy = inject(DestroyRef);
  private pending?: Subscription;
  private key = '';
  get validBalance() { return this.balance !== null && Number.isFinite(this.balance) && this.balance >= 0; }
  get difference() { return Math.round(((this.balance ?? 0) - (this.baseline() ?? 0)) * 100) / 100; }
  ngOnChanges() {
    const key = JSON.stringify([this.accountId, this.date, this.excludingId, this.refresh]);
    if (key !== this.key) { this.key = key; this.load(); }
  }
  load() {
    this.pending?.unsubscribe(); this.baseline.set(null); this.checked.emit(null); this.error.set(''); this.loading.set(false);
    if (!this.accountId || !this.date) return;
    this.loading.set(true);
    this.pending = this.api.marketBalance(this.accountId, this.date, this.excludingId).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: result => { this.loading.set(false); this.baseline.set(result.balance); this.checked.emit(result.balance); },
      error: response => { this.loading.set(false); this.error.set(response.error?.detail ?? 'The account balance couldn’t load. Check the account and date, then try again.'); },
    });
  }
}
