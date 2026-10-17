import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HouseholdSettings } from './household-settings';

describe('HouseholdSettings', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [HouseholdSettings], providers: [provideHttpClient(), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); });
  afterEach(() => { http.verify(); vi.unstubAllGlobals(); });
  it('shows member guidance without requesting owner-only invitation data', async () => {
    const f = TestBed.createComponent(HouseholdSettings); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({ id: 1, name: 'Ross', role: 'Member' });
    await Promise.resolve(); http.expectOne(r => r.url.endsWith('/members')).flush([{ id: 1, firstName: 'Alex', email: 'alex@example.test', role: 'Member', isCurrentUser: true }]);
    await vi.waitFor(() => expect(f.componentInstance.loading()).toBe(false)); f.detectChanges(); expect(f.nativeElement.textContent).toContain('Ask your household owner');
    http.expectNone(r => r.url.endsWith('/invitations')); f.destroy();
  });
  it('generates, copies, expires, and revokes owner invitations without persisting the code', async () => {
    const f = TestBed.createComponent(HouseholdSettings); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({ id: 1, name: 'Ross', role: 'Owner' }); await Promise.resolve();
    http.expectOne(r => r.url.endsWith('/members')).flush([{ id: 1, firstName: 'Alex', email: 'alex@example.test', role: 'Owner', isCurrentUser: true }]);
    http.expectOne(r => r.url.endsWith('/invitations')).flush({ hasActiveInvitation: false, expiresAt: null }); await f.whenStable();
    const c = f.componentInstance; const generate = c.generate();
    http.expectOne(r => r.method === 'POST').flush({ code: 'K7MP-4X9Q', householdName: 'Ross', expiresAt: new Date(Date.now() + 600000).toISOString() }); await generate;
    const writeText = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('navigator', { clipboard: { writeText } });
    await c.copy(true); expect(writeText).toHaveBeenCalledWith(expect.stringContaining('/signup?code=K7MP-4X9Q'));
    c.now.set(Date.now() + 700000); expect(c.active()).toBe(false); await c.copy(false); expect(writeText).toHaveBeenCalledTimes(1);
    const revoke = c.revoke(); http.expectOne(r => r.method === 'DELETE').flush(null); await revoke;
    expect(c.code()).toBe(''); expect(c.active()).toBe(false); f.destroy();
  });  it('shows owner-only removal confirmation and clears the invitation after removal', async () => {
    const f = TestBed.createComponent(HouseholdSettings); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/households/me')).flush({ id: 1, name: 'Ross', role: 'Owner' }); await Promise.resolve();
    const owner = { id: 1, firstName: 'Alex', email: 'alex@example.test', role: 'Owner' as const, isCurrentUser: true };
    const member = { id: 2, firstName: 'Jamie', email: 'jamie@example.test', role: 'Member' as const, isCurrentUser: false };
    http.expectOne(r => r.url.endsWith('/members')).flush([owner, member]);
    http.expectOne(r => r.url.endsWith('/invitations')).flush({ hasActiveInvitation: true, expiresAt: new Date(Date.now() + 600000).toISOString() });
    await vi.waitFor(() => expect(f.componentInstance.loading()).toBe(false)); f.detectChanges();
    expect(f.nativeElement.querySelectorAll('.member-card')).toHaveLength(2);
    expect(f.nativeElement.querySelectorAll('.remove-member')).toHaveLength(1);
    f.nativeElement.querySelector('.remove-member').click(); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Remove Jamie from the household?');
    http.expectNone(r => r.method === 'DELETE');
    const c = f.componentInstance; c.code.set('K7MP-4X9Q'); const removing = c.remove();
    const request = http.expectOne(r => r.method === 'DELETE'); expect(request.request.url).toContain('/members/2'); request.flush(null); await removing;
    expect(c.members()).toEqual([owner]); expect(c.code()).toBe(''); expect(c.active()).toBe(false);
    expect(c.pendingRemoval()).toBeNull(); f.destroy();
  });

});
