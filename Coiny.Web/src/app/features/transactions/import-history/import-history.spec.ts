import { Component, Input, output } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController,provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute,provideRouter,convertToParamMap } from '@angular/router';
import { ImportHistory } from './import-history';
import { EditTransaction } from '../edit-transaction/edit-transaction';
import { ImportUndoPreview } from '../../../shared/models/transactions/import-undo';
@Component({selector:'app-edit-transaction',template:''}) class DetailsStub { @Input() transactionId=0;readonly changed=output<'updated'|'deleted'>();readonly dismiss=output<void>(); }
const file={batchId:42,filename:'bills.csv',accountId:1,accountName:'Bills',importedCount:2,remainingCount:2,undoneCount:0};
const row={id:1,batchId:42,accountId:1,accountName:'Bills',date:'2026-10-16',amount:100,description:'Utilities',merchant:'Talquin',changedSinceImport:false,originalStateUnavailable:false,pairedTransactionId:null,pairedAccountName:null,pairedBatchId:null,hasPaymentLink:false,hasBudgetAssignment:false};
const review:ImportUndoPreview={batchIds:[42],relatedFiles:[file],rows:[row,{...row,id:2,description:'Edited entry',changedSinceImport:true}],removeTransactionIds:[1],accounts:[{accountId:1,name:'Bills',before:800,after:900}],checkpoints:[],paymentLinksToDetach:0,budgetAssignmentsToRemove:0,transferPairsToDetach:0,counterpartsPreserved:0,fingerprint:'abc',undoHistory:[]};
describe('ImportHistory',()=>{
 let http:HttpTestingController;
 beforeAll(()=>{Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,value(){}});Object.defineProperty(HTMLDialogElement.prototype,'close',{configurable:true,value(){}});});
 afterAll(()=>{Reflect.deleteProperty(HTMLDialogElement.prototype,'showModal');Reflect.deleteProperty(HTMLDialogElement.prototype,'close');});
 beforeEach(()=>{
  TestBed.configureTestingModule({imports:[ImportHistory],providers:[provideRouter([]),provideHttpClient(),provideHttpClientTesting(),{provide:ActivatedRoute,useValue:{snapshot:{queryParamMap:convertToParamMap({})}}}]});
  TestBed.overrideComponent(ImportHistory,{remove:{imports:[EditTransaction]},add:{imports:[DetailsStub]}});http=TestBed.inject(HttpTestingController);
 });
 afterEach(()=>http.verify());
 function render(){const f=TestBed.createComponent(ImportHistory);http.expectOne(r=>r.method==='GET' && r.url.endsWith('/transaction-imports')).flush([{...file,submissionId:'s',completedAt:'2026-10-16T12:00:00Z',balanceAfter:800}]);f.detectChanges();f.nativeElement.querySelector('.history-row button').click();http.expectOne(r=>r.url.endsWith('/42/review')).flush(review);f.detectChanges();return f;}
 it('requires changed-row, payment-link, and checkpoint acknowledgments before atomic undo',()=>{
  const f=render();const c=f.componentInstance;expect([...c.selected]).toEqual([1]);c.selectAll();c.checkUndo();
  const req=http.expectOne(r=>r.url.endsWith('/undo/preview'));expect(req.request.body.removeTransactionIds).toEqual([1,2]);
  req.flush({...review,removeTransactionIds:[1,2],paymentLinksToDetach:1,checkpoints:[{id:4,accountId:1,accountName:'Bills',statementDate:'2026-10-20',statementBalance:800}]});f.detectChanges();
  expect(c.canConfirm).toBe(false);c.confirm();http.expectNone(r=>r.url.endsWith('/undo'));
  c.changedAck=true;c.paymentAck=true;c.reconciliationAck=true;c.confirm();expect(c.canLeave()).toBe(false);
  const undo=http.expectOne(r=>r.url.endsWith('/undo'));expect(undo.request.body).toMatchObject({batchIds:[42],removeTransactionIds:[1,2],reviewedFingerprint:'abc',acknowledgeChangedTransactions:true,acknowledgePaymentLinks:true,acknowledgeReconciliations:true});
  undo.flush({operationId:undo.request.body.operationId,completedAt:'2026-10-16T12:00:00Z',removedCount:2,accounts:review.accounts,invalidatedCheckpoints:1});
  http.expectOne(r=>r.method==='GET' && r.url.endsWith('/transaction-imports')).flush([{...file,undoneCount:2}]);f.detectChanges();expect(c.canLeave()).toBe(true);expect(c.review()).toBeNull();
 });
 it('opens existing transaction details for correction and refreshes after an edit',()=>{
  const f=render();f.nativeElement.querySelector('.transaction-detail').click();f.detectChanges();
  const details=f.debugElement.query(By.directive(DetailsStub)).componentInstance as DetailsStub;expect(details.transactionId).toBe(1);details.changed.emit('updated');
  http.expectOne(r=>r.url.endsWith('/42/review')).flush({...review,rows:[{...row,changedSinceImport:true}],removeTransactionIds:[]});f.detectChanges();
  expect(f.componentInstance.selected.size).toBe(0);expect(f.componentInstance.transactionId()).toBeNull();
 });
 it('lets the user review a whole session while keeping changed entries unselected',()=>{
  const f=render();const related=[file,{...file,batchId:43,filename:'spending.csv',accountId:2}];f.componentInstance.review.set({...review,relatedFiles:related});f.detectChanges();
  const toggle=f.nativeElement.querySelector('.check input') as HTMLInputElement;toggle.click();
  const req=http.expectOne(r=>r.url.endsWith('/undo/preview'));expect(req.request.body).toEqual({batchIds:[42,43],removeTransactionIds:null});req.flush({...review,batchIds:[42,43],relatedFiles:related});
  expect(f.componentInstance.wholeSession).toBe(true);expect([...f.componentInstance.selected]).toEqual([1]);
 });
 it('retries a failed undo with the same operation ID and rejects stale previews',()=>{
  const f=render();const c=f.componentInstance;c.checkUndo();http.expectOne(r=>r.url.endsWith('/undo/preview')).flush(review);f.detectChanges();c.confirm();
  const first=http.expectOne(r=>r.url.endsWith('/undo'));const id=first.request.body.operationId;first.flush({detail:'Retry safely.'},{status:500,statusText:'Error'});c.confirm();
  const retry=http.expectOne(r=>r.url.endsWith('/undo'));expect(retry.request.body.operationId).toBe(id);retry.flush({detail:'Refresh preview.'},{status:409,statusText:'Conflict'});expect(c.preview()).toBeNull();expect(c.error()).toBe('Refresh preview.');
 });
});
