import { Component, ComponentRef, DestroyRef, HostListener, inject, OnDestroy, OnInit, signal, ViewChild, ViewContainerRef } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountService } from '../../../core/services/account.service';
import { TransactionImportService } from '../../../core/services/transaction-import.service';
import { ToastService, notificationSignal } from '../../../core/services/toast.service';
import { Account } from '../../../shared/models/account/account';
import { TransactionImportRequest } from '../../../shared/models/transactions/transaction-import';
import { ImportSessionRequest, ImportSessionResult, ImportSessionValidation, SessionReference, SessionSuggestion } from '../../../shared/models/transactions/import-session';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';
import { ImportTransactions } from '../import-transactions/import-transactions';

interface FileDraft { id:number; file:File; accountId:number; request:TransactionImportRequest|null; }
@Component({
  selector:'app-import-session', imports:[FormsModule,RouterLink,SelectMenu,CurrencyPipe,DatePipe],
  templateUrl:'./import-session.html',styleUrl:'./import-session.css',
})
export class ImportSession implements OnInit,OnDestroy {
  private readonly accountsApi=inject(AccountService);
  private readonly api=inject(TransactionImportService);
  private readonly toast=inject(ToastService);
  private readonly destroy=inject(DestroyRef);
  private readonly initialAccount=Number(inject(ActivatedRoute).snapshot.queryParamMap.get('accountId'))||0;
  @ViewChild('fileHost',{read:ViewContainerRef,static:true}) private host!:ViewContainerRef;
  private editors=new Map<number,ComponentRef<ImportTransactions>>();
  private sequence=0;
  private sessionId=crypto.randomUUID();
  private pending:ImportSessionRequest|null=null;
  readonly accounts=signal<Account[]>([]);
  readonly loading=signal(true);
  readonly busy=signal(false);
  readonly childBusy=signal(false);
  readonly files=signal<FileDraft[]>([]);
  readonly stage=signal<'files'|'review'|'transfers'|'confirm'|'done'>('files');
  readonly active=signal(0);
  readonly error=notificationSignal('error');
  readonly validation=signal<ImportSessionValidation|null>(null);
  readonly result=signal<ImportSessionResult|null>(null);
  readonly accepted=signal<SessionSuggestion[]>([]);
  confirmed=false;
  page=1;
  get locked(){return this.pending!==null;}
  get anyBusy(){return this.busy()||this.childBusy();}
  get ready(){return this.files().length>0&&this.files().every(f=>f.request);}
  get hasMatchingStep(){return !this.validation()||this.suggestions.length>0;}
  get totalSteps(){return this.hasMatchingStep?4:3;}
  get step(){return this.stage()==='files'?1:this.stage()==='review'?2:this.stage()==='transfers'?3:this.totalSteps;}
  get suggestions(){return this.validation()?.suggestions??[];}
  get pageCount(){return Math.max(1,Math.ceil(this.suggestions.length/10));}
  get shownSuggestions(){return this.suggestions.slice((this.page-1)*10,this.page*10);}
  get totalRows(){return this.files().reduce((n,f)=>n+(f.request?.rows.length??0),0);}
  get unmatchedTransfers(){const matched=new Set(this.accepted().flatMap(s=>[this.key(s.first.reference),this.key(s.second.reference)]));return this.files().reduce((n,f)=>n+(f.request?.rows.filter(r=>r.categoryType==='Transfer'&&!matched.has(`r:${f.accountId}:${r.rowId}`)).length??0),0);}
  ngOnInit(){this.loadAccounts();}
  loadAccounts(){this.loading.set(true);this.error.set('');this.accountsApi.getAccounts().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:rows=>{this.accounts.set(rows.filter(a=>a.isActive));this.loading.set(false);},error:()=>{this.loading.set(false);this.error.set('Accounts couldn’t load. Please try again.');}});}
  rowCount(id:number){return this.files().find(f=>f.accountId===id)?.request?.rows.length??0;}
  accountName(id:number){return this.accounts().find(a=>a.id===id)?.name??'Account';}
  accountOptions(file:FileDraft):SelectOption[]{return [{value:0,label:'Choose account'},...this.accounts().filter(a=>a.id===file.accountId||!this.files().some(f=>f.id!==file.id&&f.accountId===a.id)).map(a=>({value:a.id,label:a.name,detail:a.institutionName??'No institution'}))];}
  addFiles(event:Event){
    if(this.anyBusy||this.locked||this.stage()!=='files')return;
    const input=event.target as HTMLInputElement;const selected=Array.from(input.files??[]);input.value='';
    if(this.files().length+selected.length>8){this.error.set('Choose up to 8 files in one session.');return;}
    if(selected.some(f=>f.size>2*1024*1024)){this.error.set('Each CSV must be 2 MB or less.');return;}
    const additions=selected.map(file=>({id:++this.sequence,file,accountId:0,request:null} as FileDraft));
    if(!this.files().length&&additions.length&&this.accounts().some(a=>a.id===this.initialAccount))additions[0].accountId=this.initialAccount;
    this.files.update(rows=>[...rows,...additions]);this.error.set('');
  }
  remove(file:FileDraft){if(!this.anyBusy&&!this.locked)this.files.update(rows=>rows.filter(f=>f!==file));}
  start(){
    if(this.anyBusy||this.locked)return;
    const files=this.files();if(!files.length||files.some(f=>!f.accountId)||new Set(files.map(f=>f.accountId)).size!==files.length){this.error.set('Choose a different Coiny account for each file.');return;}
    this.error.set('');this.stage.set('review');this.reviewFile(0);
  }
  reviewFile(index:number){
    if(this.anyBusy||this.locked)return;
    const file=this.files()[index];if(!file)return;
    this.stage.set('review');this.active.set(index);this.error.set('');
    if(this.host.length)this.host.detach(0);
    let editor=this.editors.get(file.id);
    if(!editor){
      editor=this.host.createComponent(ImportTransactions);this.editors.set(file.id,editor);
      editor.setInput('sessionMode',true);editor.setInput('sessionFile',file.file);editor.setInput('sessionAccountId',file.accountId);
      editor.instance.sessionBusy.subscribe(value=>this.childBusy.set(value));
      editor.instance.reviewed.subscribe(request=>this.fileReviewed(file.id,request));
      editor.instance.reviewInvalidated.subscribe(()=>this.invalidateFile(file.id));
    }else this.host.insert(editor.hostView);
    window.scrollTo({top:0,behavior:'instant'});
  }
  private invalidateFile(id:number){this.files.update(rows=>rows.map(f=>f.id===id?{...f,request:null}:f));this.validation.set(null);this.accepted.set([]);this.confirmed=false;}
  private fileReviewed(id:number,request:TransactionImportRequest){
    this.files.update(rows=>rows.map(f=>f.id===id?{...f,request:structuredClone(request)}:f));
    const next=this.files().findIndex(f=>!f.request);
    if(next>=0)this.reviewFile(next);else this.checkSession();
  }
  backToFiles(){
    if(this.anyBusy||this.locked||!window.confirm('Return to files? Column mappings and transaction reviews will be cleared.'))return;
    this.clearEditors();this.files.update(rows=>rows.map(f=>({...f,request:null})));this.validation.set(null);this.accepted.set([]);this.sessionId=crypto.randomUUID();this.stage.set('files');
  }
  checkSession(){
    if(!this.ready||this.anyBusy||this.locked)return;
    if(this.totalRows>8000){this.error.set('Keep this session to 8,000 included transactions or fewer.');return;}
    this.busy.set(true);this.error.set('');this.confirmed=false;this.accepted.set([]);this.validation.set(null);
    this.api.validateSession(this.request()).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:value=>{
      this.busy.set(false);this.validation.set(value);this.accepted.set([]);this.page=1;
      if(!value.canImport){this.returnToChecks(value.files.filter(f=>!f.validation.canImport).map(f=>f.accountId));return;}
      this.stage.set(value.suggestions.length?'transfers':'confirm');window.scrollTo({top:0,behavior:'instant'});
    },error:response=>{this.busy.set(false);this.error.set(response.error?.detail??'The session couldn’t be checked. Please try again.');}});
  }
  key(ref:SessionReference){return ref.transactionId!==null?`t:${ref.transactionId}`:`r:${ref.accountId}:${ref.rowId}`;}
  selected(s:SessionSuggestion){return this.accepted().includes(s);}
  unavailable(s:SessionSuggestion){const keys=new Set(this.accepted().flatMap(m=>[this.key(m.first.reference),this.key(m.second.reference)]));return !this.selected(s)&&(keys.has(this.key(s.first.reference))||keys.has(this.key(s.second.reference)));}
  toggle(s:SessionSuggestion,checked:boolean){if(this.anyBusy||this.locked||this.unavailable(s))return;this.accepted.update(rows=>checked?[...rows,s]:rows.filter(r=>r!==s));this.confirmed=false;}
  confirmStep(){if(!this.validation()?.canImport||this.anyBusy||this.locked)return;this.confirmed=false;this.stage.set('confirm');window.scrollTo({top:0,behavior:'instant'});}
  private request():ImportSessionRequest{return{submissionId:this.sessionId,files:this.files().map(f=>f.request!),links:this.accepted().map(s=>({first:s.first.reference,second:s.second.reference}))};}
  save(){
    if(this.anyBusy||this.result()||(!this.pending&&(!this.confirmed||!this.validation()?.canImport)))return;
    this.pending??=structuredClone(this.request());this.busy.set(true);this.error.set('');
    this.api.importSession(this.pending).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:result=>{this.result.set(result);this.busy.set(false);this.pending=null;this.stage.set('done');this.toast.success(`${result.files.reduce((n,f)=>n+f.importedCount,0)} transactions imported across ${result.files.length} accounts.`);this.clearEditors();},error:response=>{
      this.busy.set(false);
      if([400,403,409,422].includes(response.status)){
        this.pending=null;this.sessionId=crypto.randomUUID();this.accepted.set([]);this.validation.set(null);this.confirmed=false;
        this.error.set(response.error?.detail??'Account data changed. Recheck the files before importing.');
        this.returnToChecks(response.status===422&&response.error?.files?response.error.files.map((f:{accountId:number})=>f.accountId):this.files().map(f=>f.accountId));
      }else this.error.set('The result is not confirmed. Keep this page open and retry this same session safely.');
    }});
  }
  private returnToChecks(accountIds:number[]){
    const ids=new Set(accountIds);this.files.update(rows=>rows.map(f=>ids.has(f.accountId)?{...f,request:null}:f));
    for(const file of this.files())if(ids.has(file.accountId))this.editors.get(file.id)?.instance.recheckReview();
    const index=this.files().findIndex(f=>!f.request);this.reviewFile(index<0?0:index);
    this.error.set('Some account checks changed. Review categories, duplicates, or balance differences again. Nothing from this session was imported.');
  }
  private clearEditors(){if(this.host?.length)this.host.detach(0);for(const editor of this.editors.values())if(!editor.hostView.destroyed)editor.destroy();this.editors.clear();}
  canLeave(){return !this.anyBusy&&(!!this.result()||!this.files().length||window.confirm(this.locked?'The import result is not confirmed. Retry this session before leaving. Leave anyway?':'Leave import? Unsaved files and reviews will be discarded.'));}
  @HostListener('window:beforeunload',['$event']) beforeUnload(event:BeforeUnloadEvent){if(!this.result()&&(this.files().length||this.anyBusy)){event.preventDefault();event.returnValue='';}}
  ngOnDestroy(){this.clearEditors();}
}
