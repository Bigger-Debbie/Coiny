import { Component, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountService } from '../../../core/services/account.service';
import { Institution, InstitutionService } from '../../../core/services/institution.service';
import { CreateAccountRequest } from '../../../shared/models/account/create-account-request';
import { getTrendDates } from '../../dashboard/trend-range';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';
import { Account } from '../../../shared/models/account/account';

@Component({
  selector: 'app-create-account',
  imports: [FormsModule, SelectMenu],
  templateUrl: './create-account.html',
  styleUrl: './create-account.css',
})
export class CreateAccount implements OnInit {
  @Input() embedded = false;
  readonly saved = output<Account>();
  readonly cancelled = output<void>();
  readonly typeOptions: SelectOption[] = ['Checking', 'Savings', 'CreditCard', 'Investment', 'Retirement', 'Loan', 'Cash', 'Crypto', 'Other']
    .map(value => ({ value, label: value === 'CreditCard' ? 'Credit card' : value }));
  get institutionOptions(): SelectOption[] {
    return [{ value: null, label: 'No institution' }, ...this.institutions().map(i => ({ value: i.id, label: i.name }))];
  }
  private readonly accountService = inject(AccountService);
  private readonly institutionService = inject(InstitutionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly institutions = signal<Institution[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly today = getTrendDates('1D').endDate;
  readonly model: CreateAccountRequest = {
    name: '', accountType: 'Checking', institutionId: null, openingBalance: 0, openingBalanceDate: this.today,
  };

  ngOnInit(): void { this.loadInstitutions(); }
  loadInstitutions(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.institutionService.getInstitutions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: institutions => { this.institutions.set(institutions.filter(i => i.isActive)); this.loading.set(false); },
      error: () => { this.loadError.set(true); this.loading.set(false); },
    });
  }

  save(form: NgForm): void {
    if (form.invalid || !this.model.name.trim() || this.model.openingBalanceDate > this.today || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.accountService.createAccount({ ...this.model, name: this.model.name.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: account => {
          this.saving.set(false);
          if (this.embedded) this.saved.emit(account);
          else void this.router.navigate(['/accounts'], { queryParams: { accountId: account.id } });
        },
        error: response => {
          this.saving.set(false);
          this.error.set(typeof response.error === 'string' ? response.error : 'Your account couldn’t be saved. Check the details and try again.');
        },
      });
  }
  cancel(): void {
    if (this.saving()) return;
    if (this.embedded) this.cancelled.emit();
    else void this.router.navigate(['/accounts']);
  }
}
