import { Component,DestroyRef,ElementRef,inject,Input,OnChanges,output,signal } from '@angular/core';
import { CurrencyPipe,DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SavingsFundsService } from '../../core/services/savings-funds.service';
import { BudgetService } from '../../core/services/budget.service';
import { ToastService } from '../../core/services/toast.service';
import { BudgetManagement,BudgetRevision } from '../../shared/models/budget/budget';
import { Modal } from '../../shared/components/modal/modal';
import { SelectMenu,SelectOption } from '../../shared/components/select-menu/select-menu';
import { SavingsAccount,SavingsFund,SavingsComponent,SaveFund,FundPreview,FundCandidate } from './savings.models';
const today=()=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/New_York',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
const palette=['#2f305f','#eb9a50','#167b83','#7b4a96','#d4a027','#486c9b','#ad3158','#64723f','#7a563c','#47777e'];
@Component({selector:'app-savings-funds',imports:[FormsModule,CurrencyPipe,DatePipe,Modal,SelectMenu],templateUrl:'./savings-funds.html',styleUrl:'./savings-funds.css'})
export class SavingsFunds implements OnChanges {
 @Input() accountId:number|null=null; @Input() draft:BudgetRevision|null=null; @Input() refreshKey=0;
 readonly changed=output<void>();
 private readonly api=inject(SavingsFundsService);private readonly budgets=inject(BudgetService);private readonly toast=inject(ToastService);private readonly destroy=inject(DestroyRef);
 readonly accounts=signal<SavingsAccount[]>([]);readonly loading=signal(false);readonly busy=signal(false);readonly error=signal('');
 readonly management=signal<BudgetManagement|null>(null);readonly dialog=signal<'edit'|'review'|'detail'|'activity'|'move'|'archive'|null>(null);readonly selected=signal<SavingsFund|null>(null);
 readonly preview=signal<FundPreview|null>(null);readonly candidates=signal<FundCandidate[]>([]);
 readonly modes:SelectOption[]=[{value:'Target',label:'Reach a target by a date'},{value:'Recurring',label:'Prepare for a recurring cost'},{value:'Monthly',label:'Build an ongoing reserve'}];
 readonly repeats:SelectOption[]=[{value:1,label:'Every month'},{value:3,label:'Every 3 months'},{value:6,label:'Every 6 months'},{value:12,label:'Every year'}];
 readonly componentIssues=signal<Record<string,Record<string,string>>>({});
 readonly fundIssues=signal<Record<string,string>>({});
 private validationAttempted=false;
 private readonly host=inject<ElementRef<HTMLElement>>(ElementRef);
 componentError(component:SavingsComponent,field:string){return this.componentIssues()[component.key]?.[field]??null;}
 componentInvalid(component:SavingsComponent){return !!Object.keys(this.componentIssues()[component.key]??{}).length;}
 private focusIssue(){requestAnimationFrame(()=>{const target=this.host.nativeElement.querySelector<HTMLElement>('input[aria-invalid="true"], [aria-invalid="true"] button, .editor-error');target?.scrollIntoView?.({block:'center',behavior:'smooth'});target?.focus({preventScroll:true});});}
 private validateEditor(focus=true){
  const issues:Record<string,Record<string,string>>={};const fund:Record<string,string>={};
  const validDate=(date:string|null)=>!!date&&/^\d{4}-\d{2}-\d{2}$/.test(date)&&Number.isFinite(Date.parse(date))&&new Date(date).toISOString().slice(0,10)===date;
  const money=(value:unknown)=>typeof value==='number'&&Number.isFinite(value)&&value>=0&&value<=999999999&&Math.abs(value*100-Math.round(value*100))<.00001;
  if(!this.form.name.trim()||this.form.name.trim().length>100)fund['name']='Enter a fund name of up to 100 characters.';
  if(!this.selectedAccount)fund['account']='Choose a savings account.';
  const earliest=this.management()?.earliestEffectiveDate??today();
  if(!validDate(this.form.plan.startDate)||this.form.plan.startDate<today()||this.form.plan.startDate<earliest)fund['startDate']='Choose today or a later date, on or after the latest budget update.';
  if(!this.form.plan.components.length||this.form.plan.components.length>30)fund['components']='Add between 1 and 30 components.';
  let openingTotal=0;
  for(const component of this.form.plan.components){
   const errors:Record<string,string>={};
   if(!component.name.trim()||component.name.trim().length>100)errors['name']='Enter a purpose of up to 100 characters.';
   if(!['Target','Recurring','Monthly'].includes(component.mode))errors['mode']='Choose how to fund this component.';
   if(!money(component.amount)||(component.mode!=='Monthly'&&component.amount<=0))errors['amount']=component.mode==='Monthly'?'Enter a nonnegative amount with up to two decimal places.':'Enter a positive amount with up to two decimal places.';
   const opening=this.selected()?component.alreadyFunded:(this.opening[component.key]??0);
   if(!money(opening))errors['opening']='Enter a nonnegative amount with up to two decimal places.';else openingTotal+=opening;
   if(component.mode!=='Monthly'){
    if(!validDate(component.targetDate))errors['targetDate']=component.mode==='Recurring'?'Choose the next expense date.':'Choose when you want to reach this goal.';
    else if(validDate(this.form.plan.startDate)){
     const latest=new Date(this.form.plan.startDate+'T00:00:00Z');latest.setUTCFullYear(latest.getUTCFullYear()+10);
     if(component.targetDate!<this.form.plan.startDate&&!(component.mode==='Target'&&opening>=component.amount))errors['targetDate']='Choose a date on or after contributions start.';
     else if(component.targetDate!>latest.toISOString().slice(0,10))errors['targetDate']='Choose a target within 10 years of the start date.';
    }
   }
   if(component.mode==='Recurring'&&![1,3,6,12].includes(Number(component.repeatMonths)))errors['repeatMonths']='Choose how often this cost repeats.';
   if(Object.keys(errors).length)issues[component.key]=errors;
  }
  if(!this.selected()&&this.selectedAccount&&openingTotal>this.selectedAccount.unallocated){
   for(const c of this.form.plan.components)if((this.opening[c.key]??0)>0)(issues[c.key]??={})['opening']='These opening allocations exceed the account’s available cash. Reduce an amount.';
  }
  this.componentIssues.set(issues);this.fundIssues.set(fund);
  const valid=!Object.keys(issues).length&&!Object.keys(fund).length;
  if(!valid&&focus)this.focusIssue();return valid;
 }
 backToEdit(){if(!this.busy()){this.error.set('');this.dialog.set('edit');}}
 form:SaveFund=this.blank();opening:Record<string,number>={};acceptedShortfall=false;private reviewed:SaveFund|null=null;
 activityKind='Contribution';activityDate=today();activityTransaction='';activityDue='';activityNote='';activityAmounts:Record<string,number>={};private operationId=crypto.randomUUID();
 moveOrigin='';moveDestination='';moveComponent='';moveAmount:number|null=null;
 private request<T>(o:import('rxjs').Observable<T>){return firstValueFrom(o.pipe(takeUntilDestroyed(this.destroy)));}
 private blank():SaveFund{return {sourceKey:null,accountId:this.accountId??0,name:'',expectedEditVersion:0,expectedRevisionId:null,draftId:null,draftEditVersion:null,plan:{startDate:today(),components:[]},openingAllocations:{}};}
 ngOnChanges(){void this.load();}
 private loadVersion=0;
 async load(){const version=++this.loadVersion;this.loading.set(true);this.error.set('');try{const accounts=await this.request(this.api.accounts(this.accountId));if(version===this.loadVersion)this.accounts.set(accounts);}catch(e){if(version===this.loadVersion)this.fail(e);}finally{if(version===this.loadVersion)this.loading.set(false);}}
 color(i:number){return palette[i]??`hsl(${(i*137.508)%360} 55% 42%)`;}
 width(account:SavingsAccount,amount:number){return Math.max(0,Math.min(100,amount/Math.max(account.balance,account.allocated,1)*100));}
 get accountOptions():SelectOption[]{return this.accounts().map(a=>({value:a.accountId,label:a.name}));}
 get selectedAccount(){return this.accounts().find(a=>a.accountId===Number(this.form.accountId));}
 get legacyOptions():SelectOption[]{const revision=this.draft??this.management()?.revision;return [{value:'',label:'Create a new budget contribution'},...(revision?.allocations??[]).filter(a=>a.allocationType==='Savings'&&!a.savingsPlan&&!this.accounts().some(account=>account.funds.some(f=>f.sourceKey===a.sourceKey))).map(a=>({value:a.sourceKey!,label:a.name}))];}
 async edit(fund:SavingsFund|null=null){
  this.error.set('');this.busy.set(true);
  try{
   if(!this.draft)this.management.set(await this.request(this.budgets.management()));
   this.selected.set(fund);this.form=this.blank();this.validationAttempted=false;this.componentIssues.set({});this.fundIssues.set({});
   this.form.expectedRevisionId=this.management()?.revision.id??null;this.form.draftId=this.draft?.id??null;this.form.draftEditVersion=this.draft?.editVersion??null;
   this.form.plan.startDate=this.management()?.earliestEffectiveDate??today();
   if(this.form.plan.startDate<today())this.form.plan.startDate=today();
   if(fund){this.form={...this.form,sourceKey:fund.sourceKey,accountId:fund.accountId,name:fund.name,expectedEditVersion:fund.editVersion,plan:{startDate:this.form.plan.startDate,components:fund.components.map(c=>({...c.definition,alreadyFunded:c.balance}))}};}
   else{this.form.accountId=this.accountId??this.accounts()[0]?.accountId??0;this.addComponent();}
   this.opening={};this.preview.set(null);this.reviewed=null;this.acceptedShortfall=false;this.dialog.set('edit');
  }catch(e){this.fail(e);}finally{this.busy.set(false);}
 }
 adopt(key:string){this.form.sourceKey=key||null;const item=(this.draft??this.management()?.revision)?.allocations.find(a=>a.sourceKey===key);if(item){this.form.name=item.name;this.form.plan.components=[{key:crypto.randomUUID(),name:item.name,mode:'Monthly',amount:item.amount,targetDate:null,repeatMonths:12,alreadyFunded:0}];}this.invalidate();}
 addComponent(){this.form.plan.components.push({key:crypto.randomUUID(),name:'',mode:'Target',amount:0,targetDate:null,repeatMonths:12,alreadyFunded:0});this.invalidate();}
 removeComponent(c:SavingsComponent){this.form.plan.components=this.form.plan.components.filter(x=>x!==c);this.invalidate();}
 invalidate(){this.preview.set(null);this.reviewed=null;this.acceptedShortfall=false;if(this.validationAttempted)this.validateEditor(false);}
 async review(){if(this.busy())return;this.validationAttempted=true;this.error.set('');if(!this.validateEditor())return;this.busy.set(true);try{const request=structuredClone(this.form);request.accountId=Number(request.accountId);request.openingAllocations=this.selected()?{}:Object.fromEntries(Object.entries(this.opening).map(([key,value])=>[key,value??0]));request.plan.components.forEach(c=>{c.repeatMonths=Number(c.repeatMonths);c.targetDate=c.targetDate||null;});const result=await this.request(this.api.preview(request));this.reviewed=request;this.preview.set(result);this.acceptedShortfall=false;this.dialog.set('review');}catch(e){this.fail(e);this.focusIssue();}finally{this.busy.set(false);}}
 async save(){if(!this.reviewed||this.busy()||(this.preview()!.largestShortfall>0&&!this.acceptedShortfall))return;this.busy.set(true);this.error.set('');try{await this.request(this.api.save({...this.reviewed,acceptShortfall:this.acceptedShortfall}));this.dialog.set(null);this.toast.success('Savings fund and contribution plan saved.');await this.load();this.changed.emit();}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 openFund(key:string){const fund=this.accounts().flatMap(a=>a.funds).find(f=>f.sourceKey===key);if(!fund)return false;this.detail(fund);return true;}
 detail(fund:SavingsFund){this.selected.set(fund);this.error.set('');this.dialog.set('detail');}
 async activity(kind:string){this.activityKind=kind;this.activityDate=today();this.activityTransaction='';this.activityNote='';this.activityDue='';this.activityAmounts={};this.operationId=crypto.randomUUID();this.candidates.set([]);this.error.set('');this.dialog.set('activity');if(!['Reallocate','Allocate'].includes(kind)){try{this.candidates.set(await this.request(this.api.candidates(this.selected()!.sourceKey,kind)));}catch(e){this.fail(e);}}}
 get transactionOptions():SelectOption[]{return [{value:'',label:this.activityKind==='Contribution'?'Record contribution without a bank link':'No transaction link'},...this.candidates().map(t=>({value:String(t.id),label:`${t.merchant||t.description} · $${t.amount.toFixed(2)}`,detail:`${t.date} · ${t.accountName}`}))];}
 chooseTransaction(id:string){this.activityTransaction=id;const t=this.candidates().find(t=>t.id===Number(id));if(t)this.activityDate=t.date;}
 async record(){if(this.busy())return;this.busy.set(true);this.error.set('');try{const amounts:Record<string,number>={};for(const [key,value]of Object.entries(this.activityAmounts)){if(value)amounts[key]=this.activityKind==='Withdrawal'?-Math.abs(value):value;}await this.request(this.api.activity(this.selected()!.sourceKey,{operationId:this.operationId,expectedEditVersion:this.selected()!.editVersion,date:this.activityDate,kind:this.activityKind,amounts,transactionId:this.activityTransaction?Number(this.activityTransaction):null,dueDate:this.activityDue||null,note:this.activityNote||null}));this.dialog.set(null);this.toast.success('Savings allocation recorded.');await this.load();this.changed.emit();}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 componentName(key:string){return this.selected()?.components.find(c=>c.definition.key===key)?.definition.name??'Component';}
 closeManagement(menu:HTMLDetailsElement,event:Event):void{if(event.target instanceof Node&&!menu.contains(event.target))menu.open=false;}
 close(){if(!this.busy())this.dialog.set(null);}
 get destinationFunds(){return this.accounts().find(a=>a.accountId===this.selected()?.accountId)?.funds.filter(f=>f.sourceKey!==this.selected()?.sourceKey&&!f.isArchived)??[];}
 get destinationOptions():SelectOption[]{return [{value:'',label:'Release to unallocated cash'},...this.destinationFunds.map(f=>({value:f.sourceKey,label:f.name}))];}
 get originOptions():SelectOption[]{return this.selected()?.components.map(c=>({value:c.definition.key,label:c.definition.name}))??[];}
 get destinationComponents():SelectOption[]{return this.destinationFunds.find(f=>f.sourceKey===this.moveDestination)?.components.map(c=>({value:c.definition.key,label:c.definition.name}))??[];}
 move(){this.moveOrigin=this.selected()?.components[0]?.definition.key??'';this.moveDestination='';this.moveComponent='';this.moveAmount=null;this.operationId=crypto.randomUUID();this.dialog.set('move');this.error.set('');}
 async saveMove(){if(this.busy())return;this.busy.set(true);this.error.set('');try{const origin=this.selected()!;const dest=this.destinationFunds.find(f=>f.sourceKey===this.moveDestination);await this.request(this.api.move(origin.sourceKey,{operationId:this.operationId,expectedEditVersion:origin.editVersion,componentKey:this.moveOrigin,destinationFundKey:dest?.sourceKey??null,destinationComponentKey:dest?this.moveComponent:null,destinationEditVersion:dest?.editVersion??null,amount:this.moveAmount}));this.dialog.set(null);await this.load();this.changed.emit();this.toast.success('Savings allocation moved.');}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 canCorrect(kind:string){return ['Contribution','ContributionLinked','Withdrawal','WithdrawalLinked','Reallocate','Allocate'].includes(kind);}
 async correct(operation:string){if(this.busy()||!window.confirm('Reverse this allocation activity? Its history will remain visible.'))return;this.busy.set(true);this.error.set('');try{const fund=await this.request(this.api.correct(this.selected()!.sourceKey,operation,this.selected()!.editVersion));this.selected.set(fund);await this.load();this.changed.emit();}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 async archiveReview(){this.busy.set(true);this.error.set('');try{if(!this.draft)this.management.set(await this.request(this.budgets.management()));this.dialog.set('archive');}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 async archive(){if(this.busy())return;this.busy.set(true);this.error.set('');try{await this.request(this.api.archive(this.selected()!.sourceKey,{expectedEditVersion:this.selected()!.editVersion,expectedRevisionId:this.management()?.revision.id??0,effectiveDate:this.management()?.earliestEffectiveDate??today(),draftId:this.draft?.id??null,draftEditVersion:this.draft?.editVersion??null}));this.dialog.set(null);await this.load();this.changed.emit();this.toast.success('Fund archived; its history is retained.');}catch(e){this.fail(e);}finally{this.busy.set(false);}}
 private fail(e:unknown){this.error.set((e as {error?:{detail?:string}}).error?.detail??'Savings funds couldn’t load or save. Please try again.');}
}
