import { Component, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CategoryService } from '../../core/services/category.service';
import { MerchantRule, MerchantRuleInput, MerchantRulesService, MatchType, RulePreview } from '../../core/services/merchant-rules.service';
import { Category } from '../../shared/models/category/category';
import { normalizeRuleText } from './rule-matching';
@Component({selector:'app-merchant-rule-editor',imports:[FormsModule,CurrencyPipe,DatePipe],template:`
  <p class="hint">Exact merchant matches take priority. Otherwise the longest matching description phrase wins. Equally specific rules that disagree require review.</p>
  @if(loading()){<p role="status">Loading categories…</p>}
  @else {<form (ngSubmit)="save()"><fieldset [disabled]="busy()">
    <label for="rule-kind">Match using</label><select id="rule-kind" name="kind" [(ngModel)]="matchType" (ngModelChange)="invalidate()"><option value="MerchantExact">Exact merchant</option><option value="DescriptionContains">Description contains</option></select>
    <label for="rule-pattern">{{ matchType==='MerchantExact'?'Merchant name':'Description phrase' }}</label><input id="rule-pattern" name="pattern" maxlength="200" [(ngModel)]="pattern" (ngModelChange)="invalidate()" required /><small>Case and punctuation are ignored. Use 3–200 characters. Full merchant names are compared; descriptions use a contains match.</small>
    <label for="rule-category">Category</label><select id="rule-category" name="category" [(ngModel)]="categoryId" (ngModelChange)="invalidate()" required><option [ngValue]="0">Choose category</option>@for(c of categories();track c.id){<option [ngValue]="c.id" [disabled]="!c.isActive">{{ c.name }} · {{ c.categoryType }}{{ c.isActive?'':' (archived)' }}</option>}</select>
    <p class="hint">Rules apply only to the matching money direction. Transfers require explicit review.</p>
    <label class="enabled"><input name="active" type="checkbox" role="switch" [(ngModel)]="isActive" />Rule enabled</label>
    <div class="actions"><button class="button-outline" type="button" [disabled]="!valid" (click)="preview()">Preview matches</button><button class="button-ghost" type="button" (click)="cancelled.emit()">Cancel</button><button class="button-primary" type="submit" [disabled]="!valid">{{ busy()?'Saving…':'Save rule' }}</button></div>
  </fieldset></form>}
  @if(error()){<p role="alert">{{ error() }}</p>@if(loadFailed()){<button type="button" class="button-outline" (click)="load()">Try again</button>}}
  @if(examples();as result){<section class="examples"><h3>Matching examples</h3><p class="hint">Showing up to 10 matches among the latest {{ result.transactionsChecked }} recorded {{ categoryType }} transactions (up to 1,000). No transactions are changed.</p>@for(t of result.examples;track t.id){<div><strong>{{ t.merchant || t.description }}</strong><small>{{ t.description }}</small><small>{{ t.date | date:'MMM d, yyyy' }} · {{ t.accountName }} · {{ t.amount | currency }}</small></div>}@empty{<p>No matches in recent recorded transactions. You can still save this rule for future imports.</p>}</section>}
`,styleUrl:'./merchant-rules.css'})
export class MerchantRuleEditor implements OnInit {
  @Input() rule:MerchantRule|null=null;@Input() preset:{pattern:string;categoryId:number;matchType?:MatchType}|null=null;
  readonly saved=output<MerchantRule>();readonly cancelled=output<void>();readonly busyChanged=output<boolean>();
  private readonly api=inject(MerchantRulesService);private readonly categoriesApi=inject(CategoryService);private readonly destroy=inject(DestroyRef);
  readonly loading=signal(true);readonly loadFailed=signal(false);readonly busy=signal(false);readonly error=signal('');readonly categories=signal<Category[]>([]);readonly examples=signal<RulePreview|null>(null);
  matchType:MatchType='MerchantExact';pattern='';categoryId=0;isActive=true;
  get categoryType(){return this.categories().find(c=>c.id===this.categoryId)?.categoryType??'';}
  get valid(){return !this.loadFailed() && normalizeRuleText(this.pattern).length>=3 && normalizeRuleText(this.pattern).length<=200 && !!this.categories().find(c=>c.id===this.categoryId && (c.isActive || !this.isActive && c.id===this.rule?.categoryId));}
  ngOnInit(){this.matchType=this.rule?.matchType??this.preset?.matchType??'MerchantExact';this.pattern=this.rule?.pattern??this.preset?.pattern??'';this.categoryId=this.rule?.categoryId??this.preset?.categoryId??0;this.isActive=this.rule?.isActive??true;this.load();}
  load(){this.loading.set(true);this.loadFailed.set(false);this.error.set('');this.categoriesApi.getCategories().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:cats=>{this.categories.set(cats.filter(c=>['Income','Expense'].includes(c.categoryType) && (c.isActive || c.id===this.rule?.categoryId)));this.loading.set(false);},error:()=>{this.loading.set(false);this.loadFailed.set(true);this.error.set('Categories couldn’t load.');}});}
  invalidate(){this.examples.set(null);}
  private input():MerchantRuleInput{return {matchType:this.matchType,pattern:this.pattern.trim(),categoryId:this.categoryId,isActive:this.isActive,expectedEditVersion:this.rule?.editVersion??0};}
  private setBusy(value:boolean){this.busy.set(value);this.busyChanged.emit(value);}
  preview(){if(!this.valid || this.busy())return;this.setBusy(true);this.error.set('');this.api.preview(this.input()).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.examples.set(r);this.setBusy(false);},error:e=>{this.error.set(e.error?.detail||'Matches couldn’t load.');this.setBusy(false);}});}
  save(){if(!this.valid || this.busy())return;this.setBusy(true);this.error.set('');this.api.save(this.rule?.id??null,this.input()).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.setBusy(false);this.saved.emit(r);},error:e=>{this.error.set(e.error?.detail||'The rule couldn’t save.');this.setBusy(false);}});}
}
