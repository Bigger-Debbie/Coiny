import { notificationSignal } from '../../../core/services/toast.service';
import { Component, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../shared/models/auth/login-request';
import { AppFooter } from '../../../shared/components/app-footer/app-footer';

@Component({
  selector: 'app-login',
  imports: [FormsModule, AppFooter, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
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
        this.authService.setToken(response.token);
        if (this.joinCode) void this.router.navigate(['/household-setup'], { queryParams: { code: this.joinCode } });
        else void this.router.navigate(['/dashboard']);
      },

      error: error => {
        this.submitting.set(false);
        this.errorMessage.set(error.status === 401
          ? 'Email or password is incorrect. Please try again.'
          : 'We couldn’t sign you in. Please try again in a moment.');
      }

    });

  }

}
