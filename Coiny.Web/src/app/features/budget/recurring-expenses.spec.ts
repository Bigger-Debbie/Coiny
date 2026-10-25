import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { RecurringExpenses } from './recurring-expenses';
import { RecurringScan } from '../../shared/models/budget/recurring';
const suggestion={key:'A'.repeat(64),merchant:'Netflix',frequency:'Monthly' as const,typicalAmount:20,minimumAmount:19,maximumAmount:21,lastSeen:'2026-10-16',nextExpected:'2026-11-16',transactionCount:3,status:'NeedsReview' as const,allocationKey:null,suggestedAllocationKey:'netflix',transactions:[{id:1,date:'2026-10-16',amount:20,accountName:'Spending',description:'NETFLIX CHARGE'}]};
const scan:RecurringScan={fromDate:'2024-04-24',throughDate:'2026-10-24',transactionsScanned:30,suggestions:[suggestion],budgetItems:[{key:'netflix',name:'Netflix subscription'}]};
describe('Recurring expenses',()=>{
  let http:HttpTestingController;
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('scans only on demand and lets the user inspect evidence before confirming a budget match',()=>{
    const f=TestBed.createComponent(RecurringExpenses);f.detectChanges();http.expectNone(r=>r.url.includes('recurring-expenses'));
    f.nativeElement.querySelector('header button').click();http.expectOne(r=>r.url.endsWith('/scan')).flush(scan);f.detectChanges();
    expect(f.nativeElement.textContent).toContain('1 to review');expect(f.nativeElement.textContent).toContain('NETFLIX CHARGE');
    expect(f.nativeElement.textContent).toContain('Possible budget match');expect(f.componentInstance.choices[suggestion.key]).toBe('netflix');
    f.componentInstance.review(suggestion,'Match');const req=http.expectOne(r=>r.url.endsWith('/review'));
    expect(req.request.body).toEqual({key:suggestion.key,action:'Match',allocationKey:'netflix'});req.flush(null);http.expectOne(r=>r.url.endsWith('/unread-count')).flush({unreadCount:0});
    http.expectOne(r=>r.url.endsWith('/scan')).flush({...scan,suggestions:[{...suggestion,status:'Covered',allocationKey:'netflix'}]});f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Nothing needs review');f.componentInstance.showReviewed.set(true);f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Covered by Netflix subscription');
  });
  it('surfaces scan/review failures and can retry without altering budget or ledger',()=>{
    const f=TestBed.createComponent(RecurringExpenses);f.componentInstance.scan();http.expectOne(r=>r.url.endsWith('/scan')).flush({}, {status:500,statusText:'Error'});f.detectChanges();
    expect(f.nativeElement.textContent).toContain('couldn’t run');f.componentInstance.scan();http.expectOne(r=>r.url.endsWith('/scan')).flush(scan);
    f.componentInstance.review(suggestion,'Dismiss');http.expectOne(r=>r.url.endsWith('/review')).flush({detail:'Scan again.'},{status:409,statusText:'Conflict'});f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Scan again.');expect(f.componentInstance.busy()).toBe(false);
  });
  it('opens a prefilled budget change for explicit user review',()=>{
    const f=TestBed.createComponent(RecurringExpenses);f.componentInstance.add(suggestion);
    expect(f.componentInstance.editor()).toEqual({isIncome:false,type:'Subscriptions',add:true,prefill:{name:'Netflix',amount:20,frequency:'Monthly',nextExpected:'2026-11-16'}});
  });
});
