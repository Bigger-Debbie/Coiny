import { Component, DestroyRef, HostListener, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TransactionImportService } from '../../../core/services/transaction-import.service';
import { ToastService } from '../../../core/services/toast.service';
import { TransactionImportResult } from '../../../shared/models/transactions/transaction-import';
import { ImportUndoPreview, ImportUndoRequest, ImportUndoRow } from '../../../shared/models/transactions/import-undo';
import { EditTransaction } from '../edit-transaction/edit-transaction';
import { Modal } from '../../../shared/components/modal/modal';
@Component({selector:'app-import-history',imports:[CurrencyPipe,DatePipe,FormsModule,RouterLink,EditTransaction,Modal],templateUrl:'./import-history.html',styleUrl:'./import-history.css'})
export class ImportHistory {
  private readonly api=inject(TransactionImportService); private readonly destroy=inject(DestroyRef); private readonly toasts=inject(ToastService);
  readonly accountId=Number(inject(ActivatedRoute).snapshot.queryParamMap.get('accountId')) || undefined;
  readonly history=signal<TransactionImportResult[]>([]);readonly review=signal<ImportUndoPreview|null>(null); readonly preview=signal<ImportUndoPreview|null>(null);
  readonly loading=signal(false); readonly busy=signal(false); readonly error=signal(''); readonly transactionId=signal<number|null>(null);
  selected=new Set<number>(); batchId:number|null=null; filter=''; wholeSession=false; changedAck=false; paymentAck=false; reconciliationAck=false;
  private pendingUndo:ImportUndoRequest|null=null;
  constructor(){this.load();}
  @HostListener('window:beforeunload',['$event']) beforeUnload(event:BeforeUnloadEvent) {if(this.busy()) {event.preventDefault();event.returnValue='';}}
  canLeave(){return !this.busy();}
  load(){this.loading.set(true);this.error.set('');this.api.history(this.accountId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:items=>{this.history.set(items);this.loading.set(false);},error:()=>{this.loading.set(false);this.error.set('Import history couldn’t load. Try again.');}});}
  open(batchId:number){if(this.busy() || this.loading())return;this.batchId=batchId;this.wholeSession=false;this.loadReview();}
  loadReview(){
    if(this.batchId===null)return; this.loading.set(true);this.error.set('');this.preview.set(null);this.review.set(null);this.selected=new Set();
    this.api.review(this.batchId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:data=>{this.review.set(data);this.selected=new Set(data.removeTransactionIds);this.loading.set(false);},error:e=>{this.loading.set(false);this.error.set(e.error?.detail ?? 'Import review couldn’t load.');}});
  }
  scope(all:boolean){
    const current=this.review();if(!current || this.busy())return;this.wholeSession=all;this.loading.set(true);this.error.set('');this.selected=new Set();
    const ids=all?current.relatedFiles.map(f=>f.batchId):[this.batchId!];
    this.api.previewUndo({batchIds:ids,removeTransactionIds:null}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:data=>{this.review.set(data);this.selected=new Set(data.removeTransactionIds);this.loading.set(false);},error:e=>{this.loading.set(false);this.error.set(e.error?.detail ?? 'Import review couldn’t load.');}});
  }
  get rows(){const term=this.filter.trim().toLowerCase();return this.review()?.rows.filter(r=>!term || `${r.description} ${r.merchant ?? ''} ${r.accountName}`.toLowerCase().includes(term)) ?? [];}
  toggle(id:number,value:boolean){if(value)this.selected.add(id);else this.selected.delete(id);this.preview.set(null);this.pendingUndo=null;}
  selectAll(){this.selected=new Set(this.review()?.rows.map(r=>r.id) ?? []);}
  safeRows(){this.selected=new Set(this.review()?.rows.filter(r=>!r.changedSinceImport && !r.originalStateUnavailable).map(r=>r.id) ?? []);}
  get flaggedSelected(){return this.preview()?.rows.some(r=>this.preview()!.removeTransactionIds.includes(r.id) && (r.changedSinceImport || r.originalStateUnavailable)) ?? false;}
  get canConfirm(){const p=this.preview();return !!p && p.removeTransactionIds.length>0 && (!this.flaggedSelected || this.changedAck) && (!p.paymentLinksToDetach || this.paymentAck) && (!p.checkpoints.length || this.reconciliationAck) && !this.busy();}
  checkUndo(){
    const data=this.review();if(!data || !this.selected.size || this.busy())return;this.loading.set(true);this.error.set('');this.changedAck=false;this.paymentAck=false;this.reconciliationAck=false;
    this.api.previewUndo({batchIds:data.batchIds,removeTransactionIds:[...this.selected].sort((a,b)=>a-b)}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:p=>{this.preview.set(p);this.pendingUndo=null;this.loading.set(false);},error:e=>{this.loading.set(false);this.error.set(e.error?.detail ?? 'Undo preview couldn’t load.');}});
  }
  confirm(){
    const p=this.preview();if(!p || !this.canConfirm)return;
    this.pendingUndo ??= {batchIds:p.batchIds,removeTransactionIds:p.removeTransactionIds,reviewedFingerprint:p.fingerprint,operationId:crypto.randomUUID(),acknowledgeChangedTransactions:this.changedAck,acknowledgePaymentLinks:this.paymentAck,acknowledgeReconciliations:this.reconciliationAck};
    this.busy.set(true);this.error.set('');
    this.api.undo(this.pendingUndo).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:result=>{this.busy.set(false);this.preview.set(null);this.pendingUndo=null;this.toasts.success(`${result.removedCount} imported transactions removed. Balances and reconciliation status updated.`);this.review.set(null);this.load();},error:e=>{this.busy.set(false);this.error.set(e.error?.detail ?? 'Undo couldn’t complete. Retry with the same reviewed selection.');if(e.status===409){this.preview.set(null);this.pendingUndo=null;}}});
  }
  edit(row:ImportUndoRow){if(!this.busy() && !this.loading())this.transactionId.set(row.id);}
  edited(){this.transactionId.set(null);this.wholeSession=false;this.loadReview();}
}
