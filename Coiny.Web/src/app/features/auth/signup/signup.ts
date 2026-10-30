import { AuthLogo } from '../../../shared/components/auth-logo/auth-logo';
import { notificationSignal } from '../../../core/services/toast.service';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/services/auth.service';
import { AppFooter } from '../../../shared/components/app-footer/app-footer';
import { CoinBackground } from '../../../shared/components/coin-background/coin-background';

@Component({ selector: 'app-signup', imports: [AuthLogo, FormsModule, RouterLink, AppFooter, CoinBackground], templateUrl: './signup.html', styleUrls: ['../login/login.css', './signup.css'] })
export class Signup {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  readonly joinCode = inject(ActivatedRoute).snapshot.queryParamMap.get('code')?.slice(0, 32) ?? '';
  readonly saving = signal(false); readonly error = notificationSignal('error'); readonly visible = signal(false);
  model = { firstName: '', email: '', password: '', confirmPassword: '' };
  get passwordValid() { return this.model.password.length >= 8 && /[a-z]/.test(this.model.password) && /[A-Z]/.test(this.model.password) && /[0-9]/.test(this.model.password) && /[^a-zA-Z0-9]/.test(this.model.password); }
  signup(form: NgForm) {
    if (form.invalid || !this.model.firstName.trim() || !this.passwordValid || this.model.password !== this.model.confirmPassword || this.saving()) return;
    this.saving.set(true); this.error.set('');
    this.auth.register({ firstName: this.model.firstName.trim(), email: this.model.email.trim(), password: this.model.password })
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: response => { this.auth.setToken(response.token); this.saving.set(false); void this.router.navigate(['/household-setup'], { queryParams: this.joinCode ? { code: this.joinCode } : {} }); },
        error: response => { this.saving.set(false); this.error.set(Array.isArray(response.error) ? response.error.map((e: { description: string }) => e.description).join(' ') : 'Your account couldn’t be created. Check the details and try again.'); },
      });
  }
}
