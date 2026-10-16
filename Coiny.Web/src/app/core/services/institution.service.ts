import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';

export interface Institution {
  id: number;
  name: string;
  website: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class InstitutionService {
  private readonly http = inject(HttpClient);
  getInstitutions() {
    return this.http.get<Institution[]>(`${environemnt.apiUrl}/institutions`);
  }
}
