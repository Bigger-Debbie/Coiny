import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MerchantRule, MerchantRulesService } from '../../core/services/merchant-rules.service';
import { ToastService } from '../../core/services/toast.service';
import { MerchantRuleEditor } from './merchant-rule-editor';
@Component({selector:'app-merchant-rules',imports:[MerchantRuleEditor],template:`
  @if(editing()){<app-merchant-rule-editor [rule]="selected()" (saved)="saved($event)" (cancelled)="editing.set(false);load()" (busyChanged)="busy.set($event)" />}
  @else {<div class="toolbar"><p class="hint">Your household’s category suggestions for CSV imports. Existing transactions stay unchanged.</p><button type="button" class="button-primary" [disabled]="busy()" (click)="selected.set(null);editing.set(true)">+ Add rule</button></div>
    @if(loading()){<p role="status">Loading merchant rules…</p>}
    <div class="rule-list" role="region" aria-label="Merchant category rules" tabindex="0">@for(r of rules();track r.id){<article><div><strong>{{ r.pattern }}</strong><small>{{ r.matchType==='MerchantExact'?'Exact merchant':'Description contains' }} → {{ r.categoryName }} · {{ r.categoryType }}</small><small>{{ !r.categoryActive?'Category archived · not applied':r.isActive?'Enabled':'Disabled' }}</small></div><div class="actions"><button type="button" class="button-outline" [disabled]="busy()" (click)="selected.set(r);editing.set(true)">Edit</button><button type="button" class="button-ghost" [disabled]="busy() || !r.categoryActive && !r.isActive" (click)="toggle(r)">{{ r.isActive?'Disable':'Enable' }}</button><button type="button" class="button-ghost" [disabled]="busy()" (click)="remove(r)">Delete</button></div></article>}@empty{ @if(!loading()){<p>No rules yet. Add one here or use “Remember this merchant” during import review.</p>} }</div>}
  @if(error()){<p role="alert">{{ error() }}</p><button type="button" class="button-outline" [disabled]="busy()" (click)="load()">Reload rules</button>}
`,styleUrl:'./merchant-rules.css'})
export class MerchantRules implements OnInit {
  private readonly api=inject(MerchantRulesService);private readonly toast=inject(ToastService);private readonly destroy=inject(DestroyRef);
  readonly rules=signal<MerchantRule[]>([]);readonly loading=signal(false);readonly busy=signal(false);readonly error=signal('');readonly editing=signal(false);readonly selected=signal<MerchantRule|null>(null);
  ngOnInit(){this.load();}
  load(){this.loading.set(true);this.error.set('');this.api.list().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.rules.set(r);this.loading.set(false);},error:()=>{this.loading.set(false);this.error.set('Merchant rules couldn’t load.');}});}
  saved(rule:MerchantRule){this.editing.set(false);this.toast.success('Merchant rule saved.');this.load();}
  toggle(r:MerchantRule){if(this.busy())return;this.busy.set(true);this.error.set('');this.api.save(r.id,{matchType:r.matchType,pattern:r.pattern,categoryId:r.categoryId,isActive:!r.isActive,expectedEditVersion:r.editVersion}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.busy.set(false);this.load();},error:e=>{this.busy.set(false);this.error.set(e.error?.detail||'The rule couldn’t update.');}});}
  remove(r:MerchantRule){if(this.busy() || !window.confirm('Delete this merchant rule? Existing transactions and import selections will stay unchanged.'))return;this.busy.set(true);this.error.set('');this.api.delete(r).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.busy.set(false);this.toast.success('Merchant rule deleted.');this.load();},error:e=>{this.busy.set(false);this.error.set(e.error?.detail||'The rule couldn’t delete.');}});}
}
