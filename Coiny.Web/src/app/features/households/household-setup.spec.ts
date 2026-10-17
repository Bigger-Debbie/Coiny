import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { NgForm } from '@angular/forms';
import { HouseholdSetup } from './household-setup';

describe('HouseholdSetup', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HouseholdSetup], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ code: 'K7MP-4X9Q' }) } } }] });
    http = TestBed.inject(HttpTestingController); vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => http.verify());
  async function render() {
    const f = TestBed.createComponent(HouseholdSetup); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({}, { status: 404, statusText: 'No household' });
    await f.whenStable(); return f;
  }
  it('previews the household without joining and redeems only after explicit confirmation', async () => {
    const f = await render(); const c = f.componentInstance; expect(c.mode()).toBe('join');
    const lookup = c.lookup({ invalid: false } as NgForm);
    http.expectOne(r => r.url.endsWith('/preview')).flush({ householdId: 12, householdName: 'Ross household', expiresAt: new Date(Date.now() + 600000).toISOString() });
    await lookup; f.detectChanges(); expect(f.nativeElement.textContent).toContain('Join Ross household?');
    http.expectNone(r => r.url.endsWith('/redeem'));
    c.code = 'CHANGED'; const joining = c.join();
    const request = http.expectOne(r => r.url.endsWith('/redeem'));
    expect(request.request.body).toEqual({ code: 'K7MP-4X9Q', householdId: 12 }); request.flush({ id: 12, name: 'Ross household', role: 'Member' });
    await joining; expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/dashboard']);
  });
  it('returns to code entry when an invitation expires after preview', async () => {
    const f = await render(); const c = f.componentInstance;
    const lookup = c.lookup({ invalid: false } as NgForm);
    http.expectOne(r => r.url.endsWith('/preview')).flush({ householdId: 12, householdName: 'Ross', expiresAt: new Date().toISOString() }); await lookup;
    const join = c.join(); http.expectOne(r => r.url.endsWith('/redeem')).flush({ detail: 'This join code is invalid or has expired.' }, { status: 404, statusText: 'Expired' }); await join;
    expect(c.preview()).toBeNull(); expect(c.error()).toContain('expired');
  });
  it('creates an owner household then opens the first-account form', async () => {
    const f = await render(); const c = f.componentInstance; c.name = ' Ross '; c.choose('create');
    const creating = c.create({ invalid: false } as NgForm);
    const request = http.expectOne(r => r.url.endsWith('/households')); expect(request.request.body).toEqual({ name: 'Ross' }); request.flush({ id: 12, name: 'Ross', role: 'Owner' });
    await creating; expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith(['/accounts'], { queryParams: { create: 'account' } });
  });
  it('does not treat a network failure as a missing household', async () => {
    const f = TestBed.createComponent(HouseholdSetup); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({}, { status: 500, statusText: 'Unavailable' });
    await f.whenStable(); f.detectChanges(); expect(f.componentInstance.loadError()).toBe(true);
    expect(f.nativeElement.querySelector('.choices')).toBeNull();
  });
});
