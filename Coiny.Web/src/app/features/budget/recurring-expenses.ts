import { AlertsService } from '../../core/services/alerts.service';
import { ActivatedRoute } from '@angular/router';
import { Component, DestroyRef, inject, OnInit, ElementRef, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environemnt } from '../../../environments/environment';
import { RecurringScan, RecurringSuggestion } from '../../shared/models/budget/recurring';
import { BudgetItemEditor } from './budget-item-editor';
import { BudgetItemSelection } from '../../shared/models/budget/budget';
import { ToastService } from '../../core/services/toast.service';
@Component({ selector:'app-recurring-expenses',imports:[CurrencyPipe,DatePipe,FormsModule,BudgetItemEditor],templateUrl:'./recurring-expenses.html',styleUrl:'./recurring-expenses.css' })
export class RecurringExpenses implements OnInit {
  private readonly alerts=inject(AlertsService);private readonly route=inject(ActivatedRoute,{optional:true});private readonly host=inject(ElementRef<HTMLElement>);
  ngOnInit() {
    this.alerts.reviewRequested.pipe(takeUntilDestroyed(this.destroy)).subscribe(()=>this.openFromAlert());
    if(this.route?.snapshot.fragment==='recurring-activity') this.openFromAlert();
  }
  private openFromAlert() { this.scan();this.host.nativeElement.scrollIntoView?.({block:'start',behavior:'smooth'}); }

  private readonly http=inject(HttpClient); private readonly destroy=inject(DestroyRef); private readonly toast=inject(ToastService);
  private readonly url=`${environemnt.apiUrl}/recurring-expenses`;
  readonly budgetChanged=output<void>(); readonly data=signal<RecurringScan | null>(null); readonly busy=signal(false); readonly error=signal('');
  readonly showReviewed=signal(false); readonly editor=signal<BudgetItemSelection | null>(null); readonly choices:Record<string,string>={};
  get visible() { return this.data()?.suggestions.filter(s=>this.showReviewed() || s.status==='NeedsReview') ?? []; }
  get pending() { return this.data()?.suggestions.filter(s=>s.status==='NeedsReview').length ?? 0; }
  frequency(value:string) { return ({Weekly:'Weekly',EveryTwoWeeks:'Every two weeks',Monthly:'Monthly',EverySixMonths:'Every six months',Yearly:'Yearly'} as Record<string,string>)[value] ?? value; }
  budgetName(key:string|null) { return this.data()?.budgetItems.find(b=>b.key===key)?.name; }
  scan() {
    if(this.busy()) return; this.busy.set(true); this.error.set('');
    this.http.post<RecurringScan>(`${this.url}/scan`,{}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:data=>{
      this.data.set(data); for(const s of data.suggestions) this.choices[s.key]=s.allocationKey ?? s.suggestedAllocationKey ?? ''; this.busy.set(false);
    },error:e=>{this.busy.set(false);this.error.set(e.error?.detail || 'The recurring scan couldn’t run. Try again.');}});
  }
  review(s:RecurringSuggestion,action:'Match'|'Dismiss'|'Reset') {
    if(this.busy()) return; this.busy.set(true); this.error.set('');
    this.http.put<void>(`${this.url}/review`,{key:s.key,action,allocationKey:action==='Match'?this.choices[s.key]:null}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{
      this.busy.set(false);this.alerts.refresh();this.toast.success(action==='Match'?'Recurring expense matched to your budget.':action==='Dismiss'?'Suggestion dismissed.':'Review cleared.');this.scan();
    },error:e=>{this.busy.set(false);this.error.set(e.error?.detail || 'The review couldn’t save. Try again.');}});
  }
  add(s:RecurringSuggestion) { this.editor.set({ isIncome:false,add:true,type:'Subscriptions',prefill:{name:s.merchant,amount:s.typicalAmount,frequency:s.frequency,nextExpected:s.nextExpected} }); }
  saved() { this.editor.set(null);this.budgetChanged.emit();this.scan(); }
}
