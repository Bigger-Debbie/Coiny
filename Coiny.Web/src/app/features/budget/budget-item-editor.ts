import { Component, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormGroup, ReactiveFormsModule, UntypedFormBuilder, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BudgetService } from '../../core/services/budget.service';
import { ToastService } from '../../core/services/toast.service';
import { BudgetAllocation, BudgetIncome, BudgetItemChange, BudgetItemReview, BudgetItemSelection, BudgetManagement, BudgetSchedule } from '../../shared/models/budget/budget';
import { Modal } from '../../shared/components/modal/modal';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';
import { BudgetScheduleEditor } from './budget-schedule';

@Component({
  selector: 'app-budget-item-editor', imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, Modal, SelectMenu, BudgetScheduleEditor],
  templateUrl: './budget-item-editor.html', styleUrl: './budget-item-editor.css',
})
export class BudgetItemEditor implements OnInit {
  @Input() selection: BudgetItemSelection | null = null;
  readonly dismiss = output<void>(); readonly saved = output<void>();
  private readonly api = inject(BudgetService); private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService); private readonly fb = inject(UntypedFormBuilder);
  readonly management = signal<BudgetManagement | null>(null); readonly busy = signal(false); readonly error = signal('');
  readonly mode = signal<'list' | 'details' | 'edit' | 'review'>('list'); readonly review = signal<BudgetItemReview | null>(null);
  item: BudgetIncome | BudgetAllocation | null = null; isIncome = false; removing = false; attempted = false;
  private reviewedRequest: BudgetItemChange | null = null;
  readonly types: SelectOption[] = [{ value:'Housing',label:'Housing' },{ value:'Bills',label:'Bill' },{ value:'Subscriptions',label:'Subscription' },{ value:'Spending',label:'Everyday spending' },{ value:'Savings',label:'Savings' },{ value:'Investments',label:'Investing' }];
  readonly form = this.fb.group({ name:['',[Validators.required,Validators.maxLength(100),Validators.pattern(/\S/)]],
    amount:[null,[Validators.required,Validators.min(0),Validators.max(9999999999999999),Validators.pattern(/^\d+(\.\d{1,2})?$/)]],
    allocationType:['Bills',Validators.required], effectiveDate:['',Validators.required], healthInsurancePerPaycheck:[null,[Validators.min(0),Validators.pattern(/^\d+(\.\d{1,2})?$/)]],
    retirementContributionPerPaycheck:[null,[Validators.min(0),Validators.pattern(/^\d+(\.\d{1,2})?$/)]] });
  get schedule() { return this.form.get('schedule') as FormGroup | null; }
  get scheduledRequired() { return this.isIncome || ['Housing','Bills','Subscriptions'].includes(this.form.value.allocationType); }
  get title() { return this.mode() === 'list' ? 'Manage budget items' : this.mode() === 'review' ? 'Review budget change' : this.item ? this.item.name : 'Add budget item'; }
  ngOnInit() { this.load(); }
  load() {
    this.busy.set(true); this.error.set('');
    this.api.management().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:data=>{
      this.management.set(data); this.busy.set(false);
      if (this.selection?.add) this.open(null,this.selection.isIncome,this.selection.type);
      else if (this.selection?.sourceKey) {
        const item=(this.selection.isIncome ? data.revision.incomeSources : data.revision.allocations).find(i=>i.sourceKey===this.selection!.sourceKey);
        if (item) this.open(item,this.selection.isIncome);
        else { this.mode.set('list'); this.error.set('This historical item is no longer in the latest plan. Its payments are retained.'); }
      }
    }, error:e=>{ this.busy.set(false); this.error.set(e.error?.detail || 'Budget items couldn’t load. Try again.'); } });
  }
  open(item: BudgetIncome | BudgetAllocation | null, income: boolean, type?: string) {
    this.item=item; this.isIncome=income; this.removing=false; this.attempted=false; this.review.set(null); this.error.set('');
    this.form.removeControl('schedule');
    this.form.reset({ name:item?.name ?? '',amount:item?.amount ?? null,allocationType:(item as BudgetAllocation)?.allocationType ?? type ?? 'Bills',
      effectiveDate:this.management()!.earliestEffectiveDate,healthInsurancePerPaycheck:(item as BudgetIncome)?.healthInsurancePerPaycheck ?? null,
      retirementContributionPerPaycheck:(item as BudgetIncome)?.retirementContributionPerPaycheck ?? null });
    if (item?.schedule || this.scheduledRequired) this.addSchedule(item?.schedule ?? undefined);
    this.mode.set(item ? 'details' : 'edit');
  }
  private addSchedule(schedule?: BudgetSchedule) {
    this.form.addControl('schedule',this.fb.group({ frequency:[schedule?.frequency ?? 'Monthly',Validators.required],startDate:[schedule?.startDate ?? this.management()!.earliestEffectiveDate,Validators.required],
      endDate:[schedule?.endDate ?? ''],dayOfMonth:[schedule?.dayOfMonth ?? 1],secondDayOfMonth:[schedule?.secondDayOfMonth ?? 15] }));
  }
  changeType() { if (this.scheduledRequired && !this.schedule) this.addSchedule(); }
  toggleSchedule(event: Event) { if ((event.target as HTMLInputElement).checked) this.addSchedule(); else this.form.removeControl('schedule'); }
  edit() { this.mode.set('edit'); }
  remove() { this.removing=true; this.mode.set('edit'); }
  back() { this.review.set(null); this.reviewedRequest=null; this.mode.set('edit'); }
  reviewChange() {
    this.attempted=true; this.error.set('');
    if ((!this.removing && this.form.invalid) || !this.form.value.effectiveDate || this.form.value.effectiveDate < this.management()!.earliestEffectiveDate) {
      this.form.markAllAsTouched(); this.error.set('Check the name, amounts, and effective date before continuing.'); return;
    }
    const value=this.form.getRawValue();
    const schedule=value.schedule ? { ...value.schedule,endDate:value.schedule.endDate || null,
      dayOfMonth:['Monthly','TwiceMonthly'].includes(value.schedule.frequency) ? value.schedule.dayOfMonth : null,
      secondDayOfMonth:value.schedule.frequency==='TwiceMonthly' ? value.schedule.secondDayOfMonth : null } : null;
    const request: BudgetItemChange={ expectedRevisionId:this.management()!.revision.id,effectiveDate:value.effectiveDate,isIncome:this.isIncome,sourceKey:this.item?.sourceKey ?? null,remove:this.removing };
    if (!this.removing) {
      if (this.isIncome) request.income={ name:value.name,amount:value.amount,schedule:schedule!,healthInsurancePerPaycheck:value.healthInsurancePerPaycheck ?? null,retirementContributionPerPaycheck:value.retirementContributionPerPaycheck ?? null };
      else request.allocation={ name:value.name,amount:value.amount,allocationType:value.allocationType,schedule };
    }
    this.busy.set(true);
    this.api.reviewItem(request).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:review=>{
      this.reviewedRequest=request; this.review.set(review); this.mode.set('review'); this.busy.set(false);
    },error:e=>{ this.busy.set(false); this.error.set(e.error?.detail || 'The change couldn’t be reviewed. Try again.'); } });
  }
  save() {
    if (!this.reviewedRequest || this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.api.changeItem(this.reviewedRequest).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next:()=>{
      this.busy.set(false); this.toast.success('Budget updated. Past and paid occurrences were preserved.'); this.saved.emit();
    },error:e=>{ this.busy.set(false); this.error.set(e.error?.detail || 'The budget couldn’t save. Try again.'); if (e.status===409) { this.review.set(null); this.reviewedRequest=null; this.mode.set('list'); this.management.set(null); } } });
  }
}
