import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { Household } from '../../shared/models/household/household';
export interface InvitationStatus { hasActiveInvitation: boolean; expiresAt: string | null; }
export interface GeneratedInvitation { code: string; householdName: string; expiresAt: string; }
export interface InvitationPreview { householdId: number; householdName: string; expiresAt: string; }
@Injectable({ providedIn: 'root' })
export class HouseholdInvitationService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/households/invitations`;
  status() { return this.http.get<InvitationStatus>(this.url); }
  generate() { return this.http.post<GeneratedInvitation>(this.url, {}); }
  revoke() { return this.http.delete<void>(this.url); }
  preview(code: string) { return this.http.post<InvitationPreview>(`${this.url}/preview`, { code }); }
  redeem(code: string, householdId: number) { return this.http.post<Household>(`${this.url}/redeem`, { code, householdId }); }
}
