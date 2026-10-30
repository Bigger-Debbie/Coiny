import { AuthLogo } from '../../../shared/components/auth-logo/auth-logo';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AppFooter } from '../../../shared/components/app-footer/app-footer';
import { environemnt } from '../../../../environments/environment';

@Component({selector: 'app-password-recovery', imports: [AuthLogo, FormsModule, RouterLink, AppFooter],
  templateUrl: './recovery.html', styleUrls: ['../login/login.css', './recovery.css']})
export class PasswordRecovery {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  readonly reset = inject(ActivatedRoute).snapshot.routeConfig?.path === 'reset-password';
  readonly codeRecovery = inject(ActivatedRoute).snapshot.routeConfig?.path === 'recover-with-code';
  readonly emailAvailable = signal<boolean | null>(null);
  readonly optionsError = signal(false);
  get choosingPassword() { return this.reset || this.codeRecovery; }
  readonly busy = signal(false); readonly error = signal(''); readonly message = signal(''); readonly done = signal(false);
  email = ''; recoveryCode = ''; password = ''; confirmPassword = ''; private token = '';
  constructor() {
    if (!this.choosingPassword) this.loadOptions();
    if (this.reset) {
      const fragment = new URLSearchParams(inject(ActivatedRoute).snapshot.fragment ?? '');
      this.email = fragment.get('email') ?? ''; this.token = fragment.get('token') ?? '';
      // Remove the reset secret from the address bar and browser's current history entry.
      history.replaceState(history.state, '', location.pathname + location.search);
      if (!this.token || !this.email) this.error.set('This reset link is incomplete. Request a new one.');
    }
  }
  loadOptions() {
    this.optionsError.set(false);
    this.http.get<{email: boolean}>(`${environemnt.apiUrl}/auth/recovery-options`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: options => this.emailAvailable.set(options.email), error: () => this.optionsError.set(true)
    });
  }
  submit(form: NgForm) {
    if (form.invalid || this.busy() || this.done()) return;
    this.error.set('');
    if (!this.choosingPassword && this.emailAvailable() !== true) return;
    if (this.choosingPassword && (this.reset && !this.token || this.password !== this.confirmPassword)) {
      this.error.set(this.reset && !this.token ? 'Request a new reset link.' : 'The passwords must match.'); return;
    }
    this.busy.set(true);
    this.http.post<{message: string}>(`${environemnt.apiUrl}/auth/${this.codeRecovery ? 'recover-with-code' : this.reset ? 'reset-password' : 'forgot-password'}`,
      this.codeRecovery ? { email: this.email, recoveryCode: this.recoveryCode, newPassword: this.password } : this.reset ? { email: this.email, token: this.token, newPassword: this.password } : { email: this.email })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({next: response => {
        this.busy.set(false); this.done.set(true); this.message.set(response.message); this.password = this.confirmPassword = this.token = this.recoveryCode = '';
      }, error: response => { this.busy.set(false); this.error.set(response.status === 429 ? 'Too many attempts. Please wait before requesting another reset.' :
        response.error?.message || response.error?.detail || 'We couldn’t complete this request. Please try again.'); }});
  }
}
