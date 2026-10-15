import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environemnt } from '../../../environments/environment';
import { Household } from '../../shared/models/household/household';

@Injectable({ providedIn: 'root' })
export class HouseholdService {
  private readonly http = inject(HttpClient);

  getCurrentHousehold(): Observable<Household> {
    return this.http.get<Household>(`${environemnt.apiUrl}/households/me`);
  }
}
