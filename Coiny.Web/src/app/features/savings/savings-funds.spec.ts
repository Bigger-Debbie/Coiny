import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController,provideHttpClientTesting } from '@angular/common/http/testing';
import { SavingsFunds } from './savings-funds';
import { SavingsAccount,SavingsFund } from './savings.models';
const component={key:'goal-1',name:'Repairs',mode:'Monthly' as const,amount:100,targetDate:null,repeatMonths:12,alreadyFunded:1000};
const fund:SavingsFund={id:1,sourceKey:'fund-1',accountId:1,name:'Car',editVersion:2,isArchived:false,plan:{startDate:'2026-10-27',components:[component]},balance:1000,components:[{definition:component,balance:1000}],activities:[]};
const account:SavingsAccount={accountId:1,name:'Emergency Fund',balance:20000,allocated:1000,unallocated:19000,shortfall:0,funds:[fund]};
describe('Savings funds',()=>{
 let http:HttpTestingController;
 beforeAll(()=>{Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,value:function(this:HTMLDialogElement){this.setAttribute('open','');}});Object.defineProperty(HTMLDialogElement.prototype,'close',{configurable:true,value:function(this:HTMLDialogElement){this.removeAttribute('open');}});});
 afterAll(()=>{Reflect.deleteProperty(HTMLDialogElement.prototype,'showModal');Reflect.deleteProperty(HTMLDialogElement.prototype,'close');});
 beforeEach(()=>{TestBed.configureTestingModule({imports:[SavingsFunds],providers:[provideHttpClient(),provideHttpClientTesting()]});http=TestBed.inject(HttpTestingController);});
 afterEach(()=>{http.verify();vi.restoreAllMocks();});
 async function render(){const f=TestBed.createComponent(SavingsFunds);f.componentRef.setInput('accountId',1);f.detectChanges();http.expectOne(r=>r.url.endsWith('/savings-funds')).flush([account]);await f.whenStable();f.detectChanges();return f;}
 it('shows the account makeup and unallocated cash without creating transactions',async()=>{const f=await render();expect(f.nativeElement.textContent).toContain('$20,000.00');expect(f.nativeElement.textContent).toContain('$19,000.00');const segment=f.nativeElement.querySelector('.fund-bar button') as HTMLElement;expect(segment.style.width).toBe('5%');http.expectNone(r=>r.method!=='GET');});
 it('requires an explicit stretch-goal choice and saves the exact reviewed contribution plan',async()=>{
  const f=await render();const c=f.componentInstance;const editing=c.edit(fund);http.expectOne(r=>r.url.endsWith('/management')).flush({revision:{id:7,allocations:[]},earliestEffectiveDate:'2026-10-27'});await editing;f.detectChanges();
  const reviewing=c.review();const preview=http.expectOne(r=>r.url.endsWith('/savings-funds/preview'));expect(preview.request.body.sourceKey).toBe('fund-1');expect(preview.request.body.openingAllocations).toEqual({});preview.flush({monthlyEquivalent:100,periods:[],largestShortfall:50,supportedMonthlyContribution:60,estimatedCompletion:null,affordableCompletion:null,warning:'Not affordable'});await reviewing;f.detectChanges();
  expect(c.dialog()).toBe('review');expect(f.nativeElement.querySelectorAll('app-modal').length).toBe(1);expect(f.nativeElement.querySelector('.component-editor')).toBeNull();
  const snapshot=structuredClone(c.form);c.backToEdit();f.detectChanges();expect(c.dialog()).toBe('edit');expect(c.form).toEqual(snapshot);expect(f.nativeElement.querySelector('.component-editor')).not.toBeNull();
  c.dialog.set('review');f.detectChanges();
  await c.save();http.expectNone(r=>r.url.endsWith('/savings-funds')&&r.method==='POST');c.acceptedShortfall=true;const saving=c.save();const save=http.expectOne(r=>r.url.endsWith('/savings-funds')&&r.method==='POST');expect(save.request.body.acceptShortfall).toBe(true);expect(save.request.body.plan).toEqual(preview.request.body.plan);save.flush(fund);await Promise.resolve();http.expectOne(r=>r.url.endsWith('/savings-funds')&&r.method==='GET').flush([account]);await saving;
 });
 it('clears the review when goal inputs change',async()=>{const f=await render();const c=f.componentInstance;c.preview.set({monthlyEquivalent:100,periods:[],largestShortfall:0,supportedMonthlyContribution:100,estimatedCompletion:null,affordableCompletion:null,warning:null});c.acceptedShortfall=true;c.invalidate();expect(c.preview()).toBeNull();expect(c.acceptedShortfall).toBe(false);});
 it('uses the expense posting date and a negative withdrawal without creating a ledger transaction',async()=>{
  const f=await render();const c=f.componentInstance;c.detail(fund);const opening=c.activity('Withdrawal');http.expectOne(r=>r.url.endsWith('/candidates')).flush([{id:42,date:'2026-10-27',amount:350,merchant:'Garage',description:'Repair',accountName:'Spending'}]);await opening;c.chooseTransaction('42');c.activityAmounts={'goal-1':350};const recording=c.record();const req=http.expectOne(r=>r.url.endsWith('/fund-1/activity'));expect(req.request.body.amounts).toEqual({'goal-1':-350});expect(req.request.body.transactionId).toBe(42);expect(req.request.body.date).toBe('2026-10-27');req.flush({...fund,balance:650});await Promise.resolve();http.expectOne(r=>r.url.endsWith('/savings-funds')).flush([{...account,funds:[{...fund,balance:650}]}]);await recording;http.expectNone(r=>r.url.includes('/transactions')&&r.method==='POST');
 });
 it('moves allocations only within the displayed savings account and retains the retry identifier',async()=>{
  const f=await render();const c=f.componentInstance;c.detail(fund);c.move();c.moveOrigin='goal-1';c.moveAmount=100;const recording=c.saveMove();const req=http.expectOne(r=>r.url.endsWith('/fund-1/move'));const operation=req.request.body.operationId;expect(req.request.body.destinationFundKey).toBeNull();req.flush({detail:'temporary'},{status:500,statusText:'Error'});await recording;const retry=c.saveMove();const again=http.expectOne(r=>r.url.endsWith('/fund-1/move'));expect(again.request.body.operationId).toBe(operation);again.flush(fund);await Promise.resolve();http.expectOne(r=>r.url.endsWith('/savings-funds')).flush([account]);await retry;
 });
 it('highlights a component missing its date, focuses that field, and makes no preview request',async()=>{
  const f=await render();const c=f.componentInstance;const editing=c.edit(fund);http.expectOne(r=>r.url.endsWith('/management')).flush({revision:{id:7,allocations:[]},earliestEffectiveDate:'2026-10-27'});await editing;
  c.form.plan.components[0].mode='Recurring';c.form.plan.components[0].targetDate=null;f.detectChanges();
  let frame:FrameRequestCallback|undefined;vi.spyOn(window,'requestAnimationFrame').mockImplementation(callback=>{frame=callback;return 1;});
  await c.review();f.detectChanges();
  http.expectNone(r=>r.url.endsWith('/savings-funds/preview'));expect(c.dialog()).toBe('edit');expect(f.nativeElement.querySelector('.component-editor.invalid')).not.toBeNull();expect(f.nativeElement.textContent).toContain('Choose the next expense date.');
  const date=f.nativeElement.querySelector('#target-0') as HTMLInputElement;const scroll=vi.fn();date.scrollIntoView=scroll;frame?.(0);expect(date.getAttribute('aria-invalid')).toBe('true');expect(document.activeElement).toBe(date);expect(scroll).toHaveBeenCalledOnce();
  c.form.plan.components[0].targetDate='2030-01-01';c.invalidate();f.detectChanges();expect(f.nativeElement.querySelector('.component-editor.invalid')).toBeNull();expect(c.componentError(c.form.plan.components[0],'targetDate')).toBeNull();
 });
 it('identifies opening allocations that exceed the selected account cash',async()=>{
  const f=await render();const c=f.componentInstance;const editing=c.edit();http.expectOne(r=>r.url.endsWith('/management')).flush({revision:{id:7,allocations:[]},earliestEffectiveDate:'2026-10-27'});await editing;
  c.form.name='Vacation';const goal=c.form.plan.components[0];goal.name='Trip';goal.mode='Monthly';goal.amount=100;c.opening[goal.key]=20000;await c.review();f.detectChanges();
  http.expectNone(r=>r.url.endsWith('/preview'));expect(f.nativeElement.querySelector('.component-editor.invalid')).not.toBeNull();expect(c.componentError(goal,'opening')).toContain('available cash');
 });

 it('keeps two main actions visible and groups management actions in a dismissible menu',async()=>{
  const f=await render();const c=f.componentInstance;c.detail(fund);f.detectChanges();
  expect(f.nativeElement.querySelectorAll('.primary-fund-actions button').length).toBe(2);
  const menu=f.nativeElement.querySelector('.fund-management') as HTMLDetailsElement;const trigger=menu.querySelector('summary')!;expect(menu.open).toBe(false);
  trigger.click();expect(menu.open).toBe(true);trigger.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true}));expect(menu.open).toBe(false);
  trigger.click();c.closeManagement(menu,{target:f.nativeElement.querySelector('.balance-heading')} as unknown as Event);expect(menu.open).toBe(false);
  trigger.click();const assign=Array.from(menu.querySelectorAll('button')).find(b=>b.textContent?.includes('Assign existing cash'))!;assign.click();f.detectChanges();expect(c.dialog()).toBe('activity');expect(c.activityKind).toBe('Allocate');expect(f.nativeElement.querySelector('.fund-management')).toBeNull();
 });

});
