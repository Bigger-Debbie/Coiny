import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController,provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute,convertToParamMap,provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { ImportSession } from './import-session';
import { ImportTransactions } from '../import-transactions/import-transactions';
import { ImportReview } from '../import-transactions/import-review';
import { ImportSessionValidation } from '../../../shared/models/transactions/import-session';

const accounts=[{id:1,name:'Bills',accountType:'Checking',institutionId:null,institutionName:'Bank',isActive:true,openingBalanceDate:'2026-10-01',openingBalance:1000,currentBalance:1000},{id:2,name:'Spending',accountType:'Checking',institutionId:null,institutionName:'Bank',isActive:true,openingBalanceDate:'2026-10-01',openingBalance:100,currentBalance:100}];
const suggestion={first:{reference:{accountId:1,rowId:2,transactionId:null,expectedEditVersion:null},accountName:'Bills',date:'2026-10-02',amount:250,direction:'Out' as const,description:'Transfer'},second:{reference:{accountId:2,rowId:2,transactionId:null,expectedEditVersion:null},accountName:'Spending',date:'2026-10-03',amount:250,direction:'In' as const,description:'Transfer'}};
const check:ImportSessionValidation={canImport:true,files:accounts.map(a=>({accountId:a.id,validation:{canImport:true,rows:[],currentBalance:a.currentBalance,projectedBalance:a.id===1?750:350}})),suggestions:[suggestion],moreSuggestions:false};
describe('Multi-account import session',()=>{
  let http:HttpTestingController;
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([]),{provide:ActivatedRoute,useValue:{snapshot:{queryParamMap:convertToParamMap({accountId:'1'})}}}]});http=TestBed.inject(HttpTestingController);vi.spyOn(window,'scrollTo').mockImplementation(()=>{});});
  afterEach(()=>http.verify());
  async function render(){const f=TestBed.createComponent(ImportSession);f.detectChanges();http.expectOne(r=>r.url.endsWith('/accounts')).flush(accounts);await f.whenStable();f.detectChanges();return f;}
  function file(name:string,amount:number){const f=new File([''],name);Object.defineProperty(f,'arrayBuffer',{value:async()=>new TextEncoder().encode(`Date,Description,Amount,Classification\n2026-10-0${amount<0?2:3},Transfer,${amount},Transfer`).buffer});return f;}
  async function add(f:Awaited<ReturnType<typeof render>>){const input=f.nativeElement.querySelector('input[type=file]') as HTMLInputElement;Object.defineProperty(input,'files',{configurable:true,value:[file('bills.csv',-250),file('spending.csv',250)]});input.dispatchEvent(new Event('change'));await f.whenStable();f.detectChanges();f.componentInstance.files()[1].accountId=2;f.changeDetectorRef.markForCheck();f.detectChanges();}
  async function loadEditor(f:Awaited<ReturnType<typeof render>>){f.detectChanges();http.expectOne(r=>r.url.endsWith('/accounts')).flush(accounts);await f.whenStable();f.detectChanges();return f.debugElement.query(By.directive(ImportTransactions)).componentInstance as ImportTransactions;}
  async function reviewFile(f:Awaited<ReturnType<typeof render>>,editor:ImportTransactions){Object.assign(editor,{dateFormat:'YYYY-MM-DD',amountColumn:2,positiveMeans:'In',confirmed:true});editor.showPreview();f.detectChanges();http.expectOne(r=>r.url.endsWith('/categories')).flush([{id:1,name:'Transfer',categoryType:'Transfer',isActive:true,isSystem:true}]);http.expectOne(r=>r.method==='GET'&&r.url.endsWith('/transaction-imports')).flush([]);http.expectOne(r=>r.url.endsWith('/merchant-rules')).flush([]);await f.whenStable();f.detectChanges();const review=f.debugElement.query(By.directive(ImportReview)).componentInstance as ImportReview;review.validate();http.expectOne(r=>r.url.endsWith('/transaction-imports/validate')).flush({canImport:true,rows:[],currentBalance:1000,projectedBalance:750});review.finalConfirmed=true;review.import();return review;}
  it('walks through two files, preserves earlier edits offscreen, reviews matches, and imports once',async()=>{
    const f=await render();await add(f);const c=f.componentInstance;c.start();const first=await loadEditor(f);await reviewFile(f,first);const second=await loadEditor(f);
    c.reviewFile(0);f.detectChanges();await f.whenStable();expect(f.debugElement.query(By.directive(ImportTransactions)).componentInstance).toBe(first);expect(f.debugElement.queryAll(By.directive(ImportTransactions))).toHaveLength(1);http.expectNone(r=>r.method==='GET');
    c.reviewFile(1);f.detectChanges();await f.whenStable();await reviewFile(f,second);
    const validation=http.expectOne(r=>r.url.endsWith('/sessions/validate'));expect(validation.request.body.files).toHaveLength(2);validation.flush(check);f.detectChanges();await f.whenStable();
    expect(c.stage()).toBe('transfers');expect(c.accepted()).toHaveLength(0);c.toggle(suggestion,true);c.confirmStep();c.confirmed=true;c.save();
    const save=http.expectOne(r=>r.url.endsWith('/sessions'));expect(save.request.body.files).toHaveLength(2);expect(save.request.body.links).toHaveLength(1);
    const id=save.request.body.submissionId;save.flush({submissionId:id,linkedPairs:1,files:accounts.map(a=>({accountId:a.id,filename:'bank.csv',importedCount:1,batchId:a.id,balanceAfter:a.id===1?750:350}))});
    expect(c.stage()).toBe('done');expect(c.canLeave()).toBe(true);http.expectNone(r=>r.url.endsWith('/transfers')||r.url.endsWith('/transaction-imports'));
  });
  it('uses the session API for one file and skips transfer matching when nothing matches',async()=>{
    const f=await render();const c=f.componentInstance;
    c.files.set([{id:1,file:file('single.csv',250),accountId:1,request:{accountId:1,submissionId:crypto.randomUUID(),filename:'single.csv',rows:[{rowId:2,date:'2026-10-02',amount:250,direction:'In',categoryType:'Income',categoryId:1,counterparty:'Unknown',description:'Pay',merchant:null,notes:null,isCleared:true,allowDuplicate:false}]}}]);
    c.checkSession();const request=http.expectOne(r=>r.url.endsWith('/sessions/validate'));expect(request.request.body.files).toHaveLength(1);
    request.flush({...check,files:[check.files[0]],suggestions:[]});f.detectChanges();await f.whenStable();
    expect(c.stage()).toBe('confirm');expect(c.totalSteps).toBe(3);expect(f.nativeElement.textContent).not.toContain('Use the single-file importer');
    expect(f.nativeElement.querySelector('.file-tabs')).toBeNull();
    c.confirmed=true;c.save();const save=http.expectOne(r=>r.url.endsWith('/sessions'));expect(save.request.body.files).toHaveLength(1);expect(save.request.body.links).toEqual([]);save.flush({submissionId:save.request.body.submissionId,files:[],linkedPairs:0});
  });
  it('still offers existing-ledger transfer matches when importing only one file',async()=>{
    const f=await render();const c=f.componentInstance;
    c.files.set([{id:1,file:file('single.csv',-250),accountId:1,request:{accountId:1,submissionId:crypto.randomUUID(),filename:'single.csv',rows:[{rowId:2,date:'2026-10-02',amount:250,direction:'Out',categoryType:'Transfer',categoryId:null,counterparty:'Unknown',description:'Transfer',merchant:null,notes:null,isCleared:true,allowDuplicate:false}]}}]);
    c.checkSession();http.expectOne(r=>r.url.endsWith('/sessions/validate')).flush({...check,files:[check.files[0]],suggestions:[{...suggestion,second:{...suggestion.second,reference:{accountId:2,rowId:null,transactionId:10,expectedEditVersion:0}}}]});
    expect(c.stage()).toBe('transfers');expect(c.totalSteps).toBe(4);expect(c.accepted()).toHaveLength(0);
  });
  it('does not offer an entry twice and keeps the same locked payload on an uncertain retry',async()=>{
    const f=await render();const c=f.componentInstance;c.validation.set(check);c.stage.set('transfers');c.toggle(suggestion,true);
    const competing={first:suggestion.first,second:{...suggestion.second,reference:{...suggestion.second.reference,rowId:3}}};expect(c.unavailable(competing)).toBe(true);c.toggle(competing,true);expect(c.accepted()).toHaveLength(1);
    c.confirmStep();c.confirmed=true;c.save();const initial=http.expectOne(r=>r.url.endsWith('/sessions'));const body=initial.request.body;initial.flush({}, {status:500,statusText:'Error'});
    expect(c.locked).toBe(true);c.save();const retry=http.expectOne(r=>r.url.endsWith('/sessions'));expect(retry.request.body).toEqual(body);retry.flush({submissionId:body.submissionId,files:[],linkedPairs:1});expect(c.stage()).toBe('done');
  });
});
