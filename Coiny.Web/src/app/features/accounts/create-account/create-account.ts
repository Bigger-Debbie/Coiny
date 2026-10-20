import { ToastService, notificationSignal } from '../../../core/services/toast.service';
import { Component, computed, DestroyRef, inject, Input, OnInit, output, signal } from '@angular/core';
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
  private readonly toasts = inject(ToastService);
  @Input() embedded = false;
  @Input() account: Account | null = null;
  readonly saved = output<Account>();
  readonly cancelled = output<void>();
  readonly typeOptions: SelectOption[] = ['Checking', 'Savings', 'CreditCard', 'Investment', 'Retirement', 'Loan', 'Cash', 'Crypto', 'Other']
    .map(value => ({ value, label: value === 'CreditCard' ? 'Credit card' : value }));
  readonly loanTypes: SelectOption[] = ['Mortgage', 'Auto', 'Personal', 'Student', 'Other'].map(value => ({ value, label: value }));
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
  readonly institutionSaving = signal(false);
  readonly busy = computed(() => this.saving() || this.institutionSaving());
  readonly addingInstitution = signal(false);
  readonly institutionError = notificationSignal('error');
  institutionName = '';
  institutionWebsite = '';
  readonly error = notificationSignal('error');
  readonly today = getTrendDates('1D').endDate;
  readonly model: CreateAccountRequest = {
    name: '', accountType: 'Checking', loanSubtype: null, institutionId: null, openingBalance: 0, openingBalanceDate: this.today,
  };

  ngOnInit(): void {
    if (this.account) Object.assign(this.model, { name: this.account.name, accountType: this.account.accountType,
      loanSubtype: this.account.loanSubtype, institutionId: this.account.institutionId,
      openingBalance: this.account.openingBalance, openingBalanceDate: this.account.openingBalanceDate });
    this.loadInstitutions();
  }
  loadInstitutions(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.institutionService.getInstitutions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: institutions => { this.institutions.set(institutions.filter(i => i.isActive)); this.loading.set(false); },
      error: () => { this.loadError.set(true); this.loading.set(false); },
    });
  }

  save(form: NgForm): void {
    if (form.invalid || !this.model.name.trim() || this.model.openingBalanceDate > this.today || this.busy() || this.addingInstitution()) return;
    this.saving.set(true);
    this.error.set('');
    const request = { ...this.model, loanSubtype: this.model.accountType === 'Loan' ? this.model.loanSubtype ?? 'Other' : null, name: this.model.name.trim() };
    const operation = this.account ? this.accountService.updateAccount(this.account.id, request) : this.accountService.createAccount(request);
    operation
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: account => {
          this.saving.set(false);
          if (!this.embedded) this.toasts.success(this.account ? 'Account updated.' : 'Account created.');
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
    if (this.busy()) return;
    if (this.embedded) this.cancelled.emit();
    else void this.router.navigate(['/accounts']);
  }
  closeInstitution(): void {
    if (this.busy()) return;
    this.addingInstitution.set(false);
    this.institutionName = '';
    this.institutionWebsite = '';
    this.institutionError.set('');
  }
  saveInstitution(): void {
    if (this.busy()) return;
    const name = this.institutionName.trim();
    const website = this.institutionWebsite.trim();
    if (!name || name.length > 100) {
      this.institutionError.set('Enter an institution name of up to 100 characters.'); return;
    }
    if (website && (website.length > 250 || !this.validWebsite(website))) {
      this.institutionError.set('Enter a website starting with https:// or http://, or leave it blank.'); return;
    }
    this.institutionSaving.set(true);
    this.institutionError.set('');
    this.institutionService.createInstitution({ name, website: website || null })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: institution => { this.toasts.success('Institution created.');
          this.institutions.update(items => [...items, institution].sort((a, b) => a.name.localeCompare(b.name)));
          this.model.institutionId = institution.id;
          this.institutionSaving.set(false);
          this.closeInstitution();
        },
        error: () => {
          this.institutionSaving.set(false);
          this.institutionError.set('Your institution couldn’t be saved. Please try again.');
        },
      });
  }
  private validWebsite(value: string): boolean {
    try { return ['http:', 'https:'].includes(new URL(value).protocol); }
    catch { return false; }
  }
}
