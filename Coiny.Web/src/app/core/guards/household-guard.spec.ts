import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, Observable, of, throwError } from 'rxjs';
import { HouseholdService } from '../services/household.service';
import { householdGuard } from './household-guard';

describe('householdGuard', () => {
  function setup(result: Observable<unknown>) {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: HouseholdService, useValue: { getCurrentHousehold: () => result } }] });
  }
  async function check() { return firstValueFrom(TestBed.runInInjectionContext(() => householdGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as Observable<unknown>); }
  it('allows financial routes for a household member', async () => { setup(of({ id: 1 })); expect(await check()).toBe(true); });
  it.each([404, 500])('routes status %s to the household check screen', async status => {
    setup(throwError(() => ({ status })));
    expect(TestBed.inject(Router).serializeUrl(await check() as import('@angular/router').UrlTree)).toBe('/household-setup');
  });
  it('returns unauthorized users to login', async () => {
    setup(throwError(() => ({ status: 401 })));
    expect(TestBed.inject(Router).serializeUrl(await check() as import('@angular/router').UrlTree)).toBe('/login');
  });
});
