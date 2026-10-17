import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { HouseholdService } from '../services/household.service';

export const householdGuard: CanActivateChildFn = () => {
  const households = inject(HouseholdService); const router = inject(Router);
  return households.getCurrentHousehold().pipe(map(() => true), catchError(error => of(
    router.createUrlTree([error.status === 401 ? '/login' : '/household-setup'])
  )));
};
