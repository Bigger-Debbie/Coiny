import { Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { SelectMenu, SelectOption } from '../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-budget-schedule', imports: [ReactiveFormsModule, SelectMenu],
  template: `
    <div class="schedule" [formGroup]="group">
      <div><label [for]="id + '-frequency'">Frequency</label>
        <app-select-menu [inputId]="id + '-frequency'" label="Frequency" formControlName="frequency" [options]="frequencies" /></div>
      <div><label [for]="id + '-start'">{{ anchored ? (income ? 'First payday' : 'First payment date') : 'Schedule starts on' }}</label>
        <input [id]="id + '-start'" type="date" formControlName="startDate" [attr.aria-describedby]="id + '-help'" required /></div>
      @if (!anchored) {
        <div><label [for]="id + '-day'">{{ income ? 'Payday each month' : 'Payment day each month' }}</label><input [id]="id + '-day'" type="number" min="1" max="31" step="1" formControlName="dayOfMonth" /></div>
        @if (group.value.frequency === 'TwiceMonthly') {
          <div><label [for]="id + '-second'">{{ income ? 'Second payday each month' : 'Second payment day each month' }}</label><input [id]="id + '-second'" type="number" min="1" max="31" step="1" formControlName="secondDayOfMonth" /></div>
        }
      }
      <div><label [for]="id + '-end'">Ends on (optional)</label><input [id]="id + '-end'" type="date" formControlName="endDate" /></div>
    </div>
    <small [id]="id + '-help'">
      @if (weekly) {
        {{ group.value.frequency === 'Weekly' ? 'Repeats every week' : 'Repeats every two weeks' }} from your first {{ income ? 'payday' : 'payment date' }}. Choose a date you know is correct; it sets the repeating cycle.
      } @else if (group.value.frequency === 'EverySixMonths' || group.value.frequency === 'Yearly') {
        Repeats {{ group.value.frequency === 'EverySixMonths' ? 'every six months' : 'once a year' }} from the first {{ income ? 'payday' : 'payment date' }}.
        The full amount is included in the month it is due, rather than spread across every month.
      } @else {
        “Schedule starts on” is the earliest date to include. The {{ income ? 'payday' : 'payment day' }} fields choose which days repeat each month.
        @if (group.value.frequency === 'TwiceMonthly') { For example, days 1 and 15 mean two {{ income ? 'paydays' : 'payments' }} each month. }
        @else { For example, a November 10 start with day 15 means November 15, December 15, and so on. }
      }
    </small>
    <small>Dates stay on weekends. Days missing from a month use its last day.</small>
  `,
  styles: [`.schedule { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:1rem; } small { display:block; margin-top:.75rem; color:var(--color-text-muted); } @media(max-width:600px) { .schedule { grid-template-columns:1fr; } }`],
})
export class BudgetScheduleEditor {
  @Input({ required: true }) group!: FormGroup;
  @Input() income = false;
  @Input({ required: true }) id!: string;
  readonly frequencies: SelectOption[] = [
    { value: 'Weekly', label: 'Weekly' }, { value: 'EveryTwoWeeks', label: 'Every two weeks' },
    { value: 'TwiceMonthly', label: 'Twice a month' }, { value: 'Monthly', label: 'Monthly' },
    { value: 'EverySixMonths', label: 'Every six months' }, { value: 'Yearly', label: 'Yearly' },
  ];
  get anchored() { return ['Weekly', 'EveryTwoWeeks', 'EverySixMonths', 'Yearly'].includes(this.group.value.frequency); }
  get weekly() { return ['Weekly', 'EveryTwoWeeks'].includes(this.group.value.frequency); }
}
