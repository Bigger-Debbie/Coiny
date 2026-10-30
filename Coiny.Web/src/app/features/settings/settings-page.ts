import { Component, HostListener, inject, ViewChild } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { HouseholdSettings } from '../households/household-settings';
import { UserSettings } from './user-settings';
import { AccountSecurity } from './account-security';
import { HouseholdData } from './household-data';

@Component({selector: 'app-settings-page', imports: [HouseholdSettings, UserSettings, AccountSecurity, HouseholdData],
  templateUrl: './settings-page.html', styleUrl: './settings-page.css'})
export class SettingsPage {
  private readonly auth = inject(AuthService);
  @ViewChild(HouseholdSettings) household?: HouseholdSettings;
  @ViewChild(UserSettings) preferences?: UserSettings;
  @ViewChild(AccountSecurity) security?: AccountSecurity;
  @ViewChild(HouseholdData) data?: HouseholdData;
  get busy() { return !!(this.household?.busy() || this.preferences?.busy() || this.security?.busy() || this.data?.busy()); }
  get dirty() { return !!(this.preferences?.hasChanges || this.security?.recoveryCodes().length); }
  canLeave() {
    if (!this.auth.getToken()) return true;
    const warning = this.security?.recoveryCodes().length ? 'Leave Settings? Newly generated recovery codes won’t be shown again.' : 'Leave Settings? Your unsaved preferences will be lost.';
    return !this.busy && (!this.dirty || window.confirm(warning));
  }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) {
    if (this.busy || this.dirty) { event.preventDefault(); event.returnValue = ''; }
  }
}
