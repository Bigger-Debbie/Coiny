import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environemnt } from '../../../environments/environment';
import { Household, HouseholdMember } from '../../shared/models/household/household';

@Injectable({ providedIn: 'root' })
export class HouseholdService {
  private readonly http = inject(HttpClient);

  getMembers() { return this.http.get<HouseholdMember[]>(`${environemnt.apiUrl}/households/members`); }
  removeMember(id: number) { return this.http.delete<void>(`${environemnt.apiUrl}/households/members/${id}`); }
  createHousehold(name: string): Observable<Household> {
    return this.http.post<Household>(`${environemnt.apiUrl}/households`, { name });
  }
  getCurrentHousehold(): Observable<Household> {
    return this.http.get<Household>(`${environemnt.apiUrl}/households/me`);
  }
}
