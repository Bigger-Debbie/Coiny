import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, tap } from 'rxjs';
import { environemnt } from '../../../environments/environment';
import { AuthService } from './auth.service';
export interface UserPreferences { includeMortgageInNetWorth: boolean; }
@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly events = new Subject<void>();
  readonly changed = this.events.asObservable();
  private readonly url = `${environemnt.apiUrl}/users/me/preferences`;
  get() { return this.http.get<UserPreferences>(this.url); }
  save(preferences: UserPreferences) {
    return this.http.put<UserPreferences>(this.url, preferences).pipe(tap(result => {
      this.auth.updateNetWorthPreference(result.includeMortgageInNetWorth); this.events.next();
    }));
  }
}
