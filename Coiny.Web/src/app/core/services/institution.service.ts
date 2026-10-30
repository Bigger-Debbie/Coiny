import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, tap } from 'rxjs';
import { environemnt } from '../../../environments/environment';

export interface Institution {
  id: number;
  name: string;
  website: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class InstitutionService {
  private readonly updates = new Subject<void>();
  readonly changed = this.updates.asObservable();
  updateInstitution(institution: Institution, request: { name: string; website: string | null }) {
    return this.http.put<Institution>(`${environemnt.apiUrl}/institutions/${institution.id}`, { ...request, expectedName: institution.name, expectedWebsite: institution.website }).pipe(tap(() => this.updates.next()));
  }
  private readonly http = inject(HttpClient);
  getInstitutions() {
    return this.http.get<Institution[]>(`${environemnt.apiUrl}/institutions`);
  }
  createInstitution(request: { name: string; website: string | null }) {
    return this.http.post<Institution>(`${environemnt.apiUrl}/institutions`, request);
  }
}
