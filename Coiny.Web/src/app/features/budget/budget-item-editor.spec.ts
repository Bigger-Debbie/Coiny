import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetItemEditor } from './budget-item-editor';
import { BudgetItemSelection, BudgetManagement } from '../../shared/models/budget/budget';

const item={ sourceKey:'electric',name:'Electric',amount:100,allocationType:'Bills' as const,schedule:{ frequency:'Monthly' as const,startDate:'2026-10-01',endDate:null,dayOfMonth:25,secondDayOfMonth:null } };
const management: BudgetManagement={ earliestEffectiveDate:'2026-10-23',revision:{ id:15,revisionNumber:8,editVersion:0,effectiveFromMonth:'2026-10-01',state:'Published',publishedAt:'2026-10-01',setupStep:'Review',incomeSources:[],allocations:[item] } };
const month={ month:'2026-10-01',revisionId:15,allocated:100,expectedIncome:1000,unassigned:900,overBudget:0,healthInsurance:0,retirementContributions:0,incomeSources:[],allocations:[],events:[] };
const period={ asOf:'2026-10-23',startDate:'2026-10-16',endDate:'2026-10-29',nextPayday:'2026-10-30',sourceKey:null,sources:[],bills:[],contributions:[],billsTotal:100,contributionsTotal:0,plan:month,expectedRemaining:900 };
const review={ before:month,after:{ ...month,allocated:150 },effectiveDate:'2026-10-23',protectedPayments:1,beforePeriod:period,afterPeriod:period };
describe('Budget item management',()=>{
  let http: HttpTestingController;
  beforeAll(()=>{
    Object.defineProperty(HTMLDialogElement.prototype,'showModal',{ configurable:true,value(){} });
    Object.defineProperty(HTMLDialogElement.prototype,'close',{ configurable:true,value(){} });
  });
  afterAll(()=>{ Reflect.deleteProperty(HTMLDialogElement.prototype,'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype,'close'); });
  beforeEach(()=>{ TestBed.configureTestingModule({ imports:[BudgetItemEditor],providers:[provideHttpClient(),provideHttpClientTesting()] }); http=TestBed.inject(HttpTestingController); });
  afterEach(()=>http.verify());
  function render(selection: BudgetItemSelection | null={ isIncome:false,sourceKey:'electric' }) {
    const f=TestBed.createComponent(BudgetItemEditor); f.componentRef.setInput('selection',selection); f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/management')).flush(management); f.detectChanges(); return f;
  }
  it('prefills a recurring suggestion without bypassing effective dates or review',()=>{
    const f=render({isIncome:false,type:'Subscriptions',add:true,prefill:{name:'Netflix',amount:20,frequency:'Monthly',nextExpected:'2026-10-16'}});
    const c=f.componentInstance;
    expect(c.form.value.name).toBe('Netflix'); expect(c.form.value.amount).toBe(20);
    expect(c.schedule?.value).toMatchObject({startDate:'2026-10-23',dayOfMonth:16,frequency:'Monthly'});
    c.reviewChange(); const request=http.expectOne(r=>r.url.endsWith('/items/preview'));
    expect(request.request.body.allocation).toMatchObject({name:'Netflix',amount:20,allocationType:'Subscriptions'});
    http.expectNone(r=>r.url.endsWith('/items')); request.flush(review);
  });
  it('opens details and requires a reviewed change before publication',()=>{
    const f=render(); const c=f.componentInstance; const saved=vi.fn(); c.saved.subscribe(saved);
    expect(c.mode()).toBe('details'); expect(f.nativeElement.textContent).toContain('Edit item');
    c.edit(); c.form.patchValue({ amount:150 }); c.reviewChange();
    const request=http.expectOne(r=>r.url.endsWith('/items/preview'));
    expect(request.request.body).toMatchObject({ expectedRevisionId:15,effectiveDate:'2026-10-23',sourceKey:'electric',remove:false,allocation:{ amount:150 } });
    http.expectNone(r=>r.url.endsWith('/items')); request.flush(review); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Income-period impact'); expect(f.nativeElement.textContent).toContain('already-paid');
    c.save(); http.expectOne(r=>r.url.endsWith('/items')).flush(review); expect(saved).toHaveBeenCalledOnce();
  });
  it('can add an unscheduled savings item and review removal without deleting history',()=>{
    const f=render({ isIncome:false,type:'Savings',add:true }); const c=f.componentInstance;
    expect(c.mode()).toBe('edit'); expect(c.schedule).toBeNull();
    c.form.patchValue({ name:'Tires',amount:100 }); c.reviewChange();
    const adding=http.expectOne(r=>r.url.endsWith('/preview')); expect(adding.request.body).toMatchObject({ sourceKey:null,allocation:{ name:'Tires',allocationType:'Savings',schedule:null } }); adding.flush(review);
    c.open(item,false); c.remove(); c.reviewChange();
    const removing=http.expectOne(r=>r.url.endsWith('/preview')); expect(removing.request.body).toMatchObject({ sourceKey:'electric',remove:true }); expect(removing.request.body.allocation).toBeUndefined(); removing.flush(review);
    http.expectNone(r=>r.method==='DELETE');
  });
  it('rejects past dates locally and keeps a stale review from being resubmitted',()=>{
    const f=render(); const c=f.componentInstance; c.edit(); c.form.patchValue({ effectiveDate:'2026-10-01' }); c.reviewChange();
    http.expectNone(r=>r.method==='POST'); expect(c.error()).toContain('effective date');
    c.form.patchValue({ effectiveDate:'2026-10-23' }); c.reviewChange(); http.expectOne(r=>r.url.endsWith('/preview')).flush(review);
    c.save(); http.expectOne(r=>r.url.endsWith('/items')).flush({ detail:'The household budget changed.' },{ status:409,statusText:'Conflict' });
    expect(c.mode()).toBe('list'); expect(c.review()).toBeNull(); c.save(); http.expectNone(r=>r.method==='POST');
  });
});
