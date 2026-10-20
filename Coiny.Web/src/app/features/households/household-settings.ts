import { notificationSignal } from '../../core/services/toast.service';
import { Component, DestroyRef, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HouseholdService } from '../../core/services/household.service';
import { HouseholdInvitationService, InvitationStatus } from '../../core/services/household-invitation.service';
import { Household, HouseholdMember } from '../../shared/models/household/household';
import { householdError } from './household-errors';

@Component({ selector: 'app-household-settings', imports: [DatePipe], templateUrl: './household-settings.html', styleUrl: './households.css' })
export class HouseholdSettings implements OnInit {
  private readonly households = inject(HouseholdService); private readonly invitations = inject(HouseholdInvitationService);
  private readonly destroy = inject(DestroyRef);
  readonly members = signal<HouseholdMember[]>([]);
  readonly pendingRemoval = signal<HouseholdMember | null>(null);
  readonly household = signal<Household | null>(null); readonly status = signal<InvitationStatus | null>(null);
  readonly code = signal(''); readonly loading = signal(true); readonly busy = signal(false); readonly error = notificationSignal('error'); readonly notice = notificationSignal('success');
  readonly now = signal(Date.now());
  readonly secondsLeft = computed(() => this.status()?.expiresAt ? Math.max(0, Math.ceil((Date.parse(this.status()!.expiresAt!) - this.now()) / 1000)) : 0);
  readonly timeLeft = computed(() => Math.floor(this.secondsLeft() / 60) + ':' + String(this.secondsLeft() % 60).padStart(2, '0'));
  readonly active = computed(() => !!this.status()?.hasActiveInvitation && this.secondsLeft() > 0);
  ngOnInit() { const timer = setInterval(() => this.now.set(Date.now()), 1000); this.destroy.onDestroy(() => clearInterval(timer)); void this.load(); }
  private request<T>(observable: import('rxjs').Observable<T>) { return firstValueFrom(observable.pipe(takeUntilDestroyed(this.destroy))); }
  async load() {
    this.loading.set(true); this.error.set(''); this.pendingRemoval.set(null); this.code.set('');
    try {
      const household = await this.request(this.households.getCurrentHousehold()); this.household.set(household);
      const [members, status] = await Promise.all([
        this.request(this.households.getMembers()),
        household.role === 'Owner' ? this.request(this.invitations.status()) : Promise.resolve(null),
      ]);
      this.members.set(members); this.status.set(status);
    }
    catch (e) { this.error.set(householdError(e, 'Your household couldn’t load. Please try again.')); }
    finally { this.loading.set(false); }
  }
  canRemove(member: HouseholdMember) { return this.household()?.role === 'Owner' && member.role === 'Member' && !member.isCurrentUser; }
  async remove() {
    const member = this.pendingRemoval(); if (!member || !this.canRemove(member) || this.busy()) return;
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try {
      await this.request(this.households.removeMember(member.id));
      this.members.update(members => members.filter(m => m.id !== member.id)); this.pendingRemoval.set(null);
      this.code.set(''); this.status.set({ hasActiveInvitation: false, expiresAt: null });
      this.notice.set((member.firstName || member.email) + ' was removed. Any active invitation was revoked.');
    } catch (e) { this.error.set(householdError(e, 'This member couldn’t be removed. Reload the household and try again.')); }
    finally { this.busy.set(false); }
  }
  async generate() {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.notice.set('');
    try { const result = await this.request(this.invitations.generate()); this.notice.set('Join code generated.'); this.code.set(result.code); this.now.set(Date.now()); this.status.set({ hasActiveInvitation: true, expiresAt: result.expiresAt }); }
    catch (e) { this.error.set(householdError(e, 'A join code couldn’t be generated. Please try again.')); }
    finally { this.busy.set(false); }
  }
  async revoke() {
    if (this.busy()) return; this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await this.request(this.invitations.revoke()); this.code.set(''); this.status.set({ hasActiveInvitation: false, expiresAt: null }); this.notice.set('Invitation revoked.'); }
    catch (e) { this.error.set(householdError(e, 'The invitation couldn’t be revoked. Please try again.')); }
    finally { this.busy.set(false); }
  }
  async copy(link: boolean) {
    if (this.busy() || !this.active() || !this.code()) return;
    const text = link ? new URL('/signup?code=' + encodeURIComponent(this.code()), window.location.origin).href : this.code();
    try { await navigator.clipboard.writeText(text); this.notice.set(link ? 'Invitation link copied.' : 'Join code copied.'); }
    catch { this.error.set('Copy isn’t available here. Select the code below to copy it manually.'); }
  }
}
