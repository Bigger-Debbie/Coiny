import { Component, DestroyRef, inject, OnInit, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { environemnt } from '../../../environments/environment';
interface Session { id: string; device: string; createdAt: string; expiresAt: string; isCurrent: boolean; }
@Component({selector: 'app-account-security', imports: [FormsModule, DatePipe], templateUrl: './account-security.html', styleUrl: './account-security.css'})
export class AccountSecurity implements OnInit {
  private readonly http = inject(HttpClient); private readonly destroy = inject(DestroyRef);
  private readonly auth = inject(AuthService); private readonly router = inject(Router);
  private readonly url = `${environemnt.apiUrl}/auth`;
  readonly closed = output<void>(); readonly busy = signal(false); readonly loading = signal(false);
  readonly sessions = signal<Session[]>([]); readonly error = signal(''); readonly message = signal('');
  readonly pendingSession = signal<Session | null>(null);
  readonly recoveryCodes = signal<string[]>([]); readonly codeCount = signal<number | null>(null); readonly codesError = signal('');
  private codesStatusVersion = 0;
  recoveryPassword = '';
  currentPassword = ''; newPassword = ''; confirmPassword = ''; signOutPassword = '';
  ngOnInit() { this.load(); this.loadCodeStatus(); }
  loadCodeStatus() {
    const version = ++this.codesStatusVersion;
    this.codesError.set('');
    this.http.get<{remaining: number}>(`${this.url}/recovery-codes`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: response => { if (version === this.codesStatusVersion) this.codeCount.set(response.remaining); },
      error: () => { if (version === this.codesStatusVersion) this.codesError.set('Recovery code status couldn’t load. Please try again.'); }
    });
  }
  manageCodes(form: NgForm, revoke = false) {
    if (form.invalid || this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set(''); this.codesError.set('');
    ++this.codesStatusVersion;
    this.recoveryCodes.set([]);
    this.http.post<{codes: string[]} | null>(`${this.url}/recovery-codes${revoke ? '/revoke' : ''}`, {currentPassword: this.recoveryPassword})
      .pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: response => {
          this.busy.set(false); this.recoveryPassword = ''; form.resetForm();
          this.recoveryCodes.set(response?.codes ?? []); this.codeCount.set(response?.codes.length ?? 0);
          this.message.set(revoke ? 'Recovery codes revoked.' : 'Your new recovery codes are ready. Save them now; they won’t be shown again.');
          requestAnimationFrame(() => document.getElementById('generated-recovery-codes')?.focus());
        },
        error: response => {
          this.busy.set(false); this.loadCodeStatus();
          this.showError(response.status === 429 ? 'Too many attempts. Please wait before trying again.' :
            response.error?.message || 'The request couldn’t be completed. If a new set was generated but not received, generate another set to replace it.');
        }
      });
  }
  downloadCodes() {
    if (!this.recoveryCodes().length) return;
    const text = 'Coiny recovery codes\n\nStore these privately in a password manager or another safe place.\nEach code can reset your password without email. A successful reset invalidates the entire set.\n\n' + this.recoveryCodes().join('\n') + '\n';
    const url = URL.createObjectURL(new Blob([text], {type: 'text/plain;charset=utf-8'}));
    const link = document.createElement('a'); link.href = url; link.download = 'coiny-recovery-codes.txt'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 0);
  }
  load() {
    this.loading.set(true);
    this.http.get<Session[]>(`${this.url}/sessions`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: rows => { this.sessions.set(rows); this.loading.set(false); },
      error: () => { this.showError('Sessions couldn’t load. Please try again.'); this.loading.set(false); }
    });
  }
  changePassword(form: NgForm) {
    if (form.invalid || this.busy()) return;
    if (this.newPassword !== this.confirmPassword) { this.showError('The new passwords must match.'); return; }
    this.mutate('change-password', { currentPassword: this.currentPassword, newPassword: this.newPassword });
  }
  signOutAll(form: NgForm) { if (form.valid && !this.busy()) this.mutate('logout-all', { currentPassword: this.signOutPassword }); }
  private mutate(path: string, body: object) {
    this.busy.set(true); this.error.set(''); this.message.set('');
    this.http.post(`${this.url}/${path}`, body).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => { this.busy.set(false); this.currentPassword = this.newPassword = this.confirmPassword = this.signOutPassword = '';
        this.auth.logout(); this.closed.emit(); void this.router.navigate(['/login'], { replaceUrl: true }); },
      error: response => { this.busy.set(false); this.showError(response.status === 429 ? 'Too many attempts. Please wait before trying again.' : response.error?.message || 'This change couldn’t be saved. Please try again.'); }
    });
  }
  private showError(message: string) {
    this.error.set(message);
    requestAnimationFrame(() => document.getElementById('security-error')?.focus());
  }
  revoke() {
    const session = this.pendingSession(); if (!session || this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.http.delete(`${this.url}/sessions/${encodeURIComponent(session.id)}`).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => { this.busy.set(false); this.pendingSession.set(null); this.message.set('Session signed out.'); this.load(); },
      error: () => { this.busy.set(false); this.showError('The session couldn’t be signed out. Please try again.'); }
    });
  }
}
