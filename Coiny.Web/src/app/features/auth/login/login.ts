import { AuthLogo } from '../../../shared/components/auth-logo/auth-logo';
import { notificationSignal } from '../../../core/services/toast.service';
import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../shared/models/auth/login-request';
import { AppFooter } from '../../../shared/components/app-footer/app-footer';

@Component({
  selector: 'app-login',
  imports: [AuthLogo, FormsModule, AppFooter, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  readonly mfaChallenge = signal<string | null>(null);
  mfaCode = ''; useRecoveryCode = false;
  readonly joinCode = inject(ActivatedRoute).snapshot.queryParamMap.get('code')?.slice(0, 32) ?? '';
  readonly passwordVisible = signal(false);
  readonly submitting = signal(false);
  readonly errorMessage = notificationSignal('error');
  readonly notice = signal('');
  readonly paused = signal(false);
  readonly coins = Array.from({ length: 14 }, (_, i) => ({
    left: (i * 37 + 3) % 100,
    size: 52 + (i % 4) * 13,
    duration: 28 + (i % 5) * 5,
    delay: -(i * 7 + 3),
    rotation: (i % 2 ? 1 : -1) * (12 + i * 9),
  }));

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  model: LoginRequest = {
    email: '',
    password: ''
  };

  login(form: NgForm): void {
    if (form.invalid || this.submitting()) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    this.notice.set('');

    this.authService.login(this.model).subscribe({

      next: response => {
        this.submitting.set(false);
        this.model.password = '';
        if ('requiresMfa' in response) { this.mfaChallenge.set(response.challenge); requestAnimationFrame(() => document.getElementById('login-mfa-code')?.focus()); return; }
        this.completeLogin(response.token);

      },

      error: error => {
        this.submitting.set(false);
        this.errorMessage.set(error.status === 401
          ? 'Email or password is incorrect. After several attempts, wait 15 minutes before trying again.'
          : error.status === 429 ? 'Too many attempts. Please wait before trying again.' : 'We couldn’t sign you in. Please try again in a moment.');
      }

    });

  }

  backToPassword() { this.mfaChallenge.set(null); this.model.password=''; this.mfaCode=''; this.errorMessage.set(''); }
  verifyMfa(form: NgForm) {
    if (form.invalid || this.submitting() || !this.mfaChallenge()) return;
    this.submitting.set(true); this.errorMessage.set('');
    this.authService.verifyMfa(this.mfaChallenge()!,this.mfaCode,this.useRecoveryCode).subscribe({
      next: response => { this.submitting.set(false); this.mfaCode=''; this.mfaChallenge.set(null); this.completeLogin(response.token); },
      error: error => { this.submitting.set(false); this.mfaCode=''; this.errorMessage.set(error.status===429 ? 'Too many attempts. Please wait before trying again.' : 'The code is invalid, already used, or sign-in has expired. Try a fresh code or go back to sign in again.'); }
    });
  }
  private completeLogin(token: string) {
    this.authService.setToken(token);
    if (this.joinCode) void this.router.navigate(['/household-setup'], {queryParams:{code:this.joinCode}});
    else void this.router.navigate(['/dashboard']);
  }

}
