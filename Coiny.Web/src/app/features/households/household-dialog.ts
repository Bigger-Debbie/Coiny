import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Modal } from '../../shared/components/modal/modal';
import { HouseholdSettings } from './household-settings';

// Keep existing /household links usable; the avatar opens the same content in place.
@Component({ selector: 'app-household-dialog', imports: [Modal, HouseholdSettings], template: `
  <app-modal title="Household" [busy]="settings.busy()" (dismiss)="close()"><app-household-settings #settings /></app-modal>
` })
export class HouseholdDialog {
  private readonly router = inject(Router);
  close() { void this.router.navigate(['/dashboard'], { replaceUrl: true }); }
}
