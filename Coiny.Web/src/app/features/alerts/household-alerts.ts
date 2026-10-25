import { Component, DestroyRef, inject, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AlertsPage, AlertsService, HouseholdAlert } from '../../core/services/alerts.service';
@Component({selector:'app-household-alerts',imports:[CurrencyPipe,DatePipe],template:`
  <p class="hint">Possible recurring expenses that may need a place in your household budget. Read status is personal; reviewing a pattern updates it for everyone.</p>
  @if(loading()) {<p role="status">Loading alerts…</p>}
  @if(error()) {<p role="alert">{{ error() }}</p><button class="button-outline" (click)="load()">Try again</button>}
  @if(data(); as alerts) {
    <p>{{ alerts.unreadCount }} unread · {{ alerts.total }} active alerts</p>
    @for(alert of alerts.items; track alert.id) {
      <article [class.unread]="alert.unread"><strong>{{ alert.merchant }}</strong><small>Possible {{ frequency(alert.frequency) }} expense · {{ alert.typicalAmount | currency }} typical</small><small>Next estimated {{ alert.nextExpected | date:'MMM d, yyyy':'UTC' }}</small>
        <div class="actions"><button type="button" class="button-outline" [disabled]="busy()" (click)="review(alert)">Review pattern</button>@if(alert.unread) {<button type="button" class="button-ghost" [disabled]="busy()" (click)="markRead(alert)">Mark read</button>}</div>
      </article>
    } @empty {<p>No recurring expenses need attention right now.</p>}
    @if(alerts.total>20) {<nav aria-label="Alerts pages"><button class="button-outline" [disabled]="loading() || page===1" (click)="load(page-1)">Previous</button><span>Page {{ page }}</span><button class="button-outline" [disabled]="loading() || page*20>=alerts.total" (click)="load(page+1)">Next</button></nav>}
  }`,styles:`.hint,small {color:var(--color-text-muted);font-size:.85rem;line-height:1.6;} small {display:block;margin-top:.35rem;} article {padding:1rem;margin:.75rem 0;border:1px solid var(--color-border);border-radius:var(--radius-sm);} article.unread {border-left:4px solid #287bc1;} .actions,nav {display:flex;gap:.75rem;align-items:center;flex-wrap:wrap;margin-top:.75rem;} [role=alert] {color:var(--color-danger);}`})
export class HouseholdAlerts implements OnInit {
  private readonly api=inject(AlertsService);private readonly destroy=inject(DestroyRef);private readonly router=inject(Router);
  readonly closed=output<void>();readonly data=signal<AlertsPage|null>(null);readonly loading=signal(false);readonly busy=signal(false);readonly error=signal('');page=1;
  ngOnInit() {this.load();}
  frequency(value:string) {return ({EveryTwoWeeks:'fortnightly',EverySixMonths:'six-monthly'} as Record<string,string>)[value] ?? value.toLowerCase();}
  load(page=this.page) {if(this.loading())return;this.page=page;this.loading.set(true);this.error.set('');this.api.list(page).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.data.set(r);this.api.unread.set(r.unreadCount);this.loading.set(false);},error:()=>{this.loading.set(false);this.error.set('Alerts couldn’t load.');}});}
  markRead(alert:HouseholdAlert,review=false) {
    if(this.busy())return;this.busy.set(true);this.error.set('');
    this.api.read(alert).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.busy.set(false);this.api.refresh();if(review)this.openReview();else this.load();},error:()=>{this.busy.set(false);this.error.set('The alert couldn’t be updated. Refresh and try again.');}});
  }
  review(alert:HouseholdAlert) {if(alert.unread)this.markRead(alert,true);else void this.openReview();}
  private async openReview() {if(await this.router.navigate(['/budget'],{fragment:'recurring-activity'})){this.api.review();this.closed.emit();}}
}
