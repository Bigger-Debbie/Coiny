import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { HouseholdService } from '../../core/services/household.service';
import { HouseholdInvitationService, InvitationPreview } from '../../core/services/household-invitation.service';
import { householdError } from './household-errors';
import { AppFooter } from '../../shared/components/app-footer/app-footer';

@Component({ selector: 'app-household-setup', imports: [FormsModule, DatePipe, AppFooter], templateUrl: './household-setup.html', styleUrl: './households.css' })
export class HouseholdSetup implements OnInit {
  private readonly households = inject(HouseholdService);
  private readonly invitations = inject(HouseholdInvitationService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  readonly loading = signal(true); readonly loadError = signal(false); readonly busy = signal(false); readonly error = signal('');
  readonly mode = signal<'create' | 'join' | null>(null); readonly preview = signal<InvitationPreview | null>(null);
  code = inject(ActivatedRoute).snapshot.queryParamMap.get('code')?.slice(0, 32) ?? '';
  name = ''; private previewCode = '';
  ngOnInit() { if (this.code) this.mode.set('join'); void this.check(); }
  private request<T>(observable: import('rxjs').Observable<T>) { return firstValueFrom(observable.pipe(takeUntilDestroyed(this.destroy))); }
  async check() {
    this.loading.set(true); this.loadError.set(false); this.error.set('');
    try { await this.request(this.households.getCurrentHousehold()); await this.router.navigate(['/household']); }
    catch (e) { if ((e as { status?: number }).status !== 404) { this.loadError.set(true); this.error.set('Your household status couldn’t load. Try again before creating or joining a household.'); } }
    finally { this.loading.set(false); }
  }
  choose(mode: 'create' | 'join') { this.mode.set(mode); this.error.set(''); this.preview.set(null); }
  async create(form: NgForm) {
    if (this.busy() || form.invalid || !this.name.trim()) return;
    this.busy.set(true); this.error.set('');
    try { await this.request(this.households.createHousehold(this.name.trim())); await this.router.navigate(['/accounts'], { queryParams: { create: 'account' } }); }
    catch (e) { this.error.set(householdError(e, 'Your household couldn’t be created. Please try again.')); }
    finally { this.busy.set(false); }
  }
  async lookup(form: NgForm) {
    if (this.busy() || form.invalid) return;
    this.busy.set(true); this.error.set(''); this.preview.set(null);
    try { const code = this.code.trim(); const preview = await this.request(this.invitations.preview(code)); this.previewCode = code; this.preview.set(preview); }
    catch (e) { this.error.set(householdError(e, 'This invitation couldn’t be checked. Please try again.')); }
    finally { this.busy.set(false); }
  }
  async join() {
    const preview = this.preview(); if (!preview || this.busy()) return;
    this.busy.set(true); this.error.set('');
    try { await this.request(this.invitations.redeem(this.previewCode, preview.householdId)); await this.router.navigate(['/dashboard']); }
    catch (e) { this.preview.set(null); this.error.set(householdError(e, 'You couldn’t join this household. Check the code and try again.')); }
    finally { this.busy.set(false); }
  }
  async logout() { if (this.busy()) return; if (await this.router.navigate(['/login'], { replaceUrl: true })) this.auth.logout(); }
}
