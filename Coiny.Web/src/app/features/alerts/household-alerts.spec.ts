import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { HouseholdAlerts } from './household-alerts';
import { AlertsService } from '../../core/services/alerts.service';
const alert={id:4,merchant:'Netflix',frequency:'Monthly',typicalAmount:20,nextExpected:'2026-11-16',updatedAt:'2026-10-25T12:00:00Z',unread:true,version:2};
describe('Household alerts',()=>{
  let http:HttpTestingController;
  beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]});http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  it('keeps alerts unread until an explicit read or review action and opens the recurring review panel',async()=>{
    const f=TestBed.createComponent(HouseholdAlerts);f.detectChanges();http.expectOne(r=>r.url.endsWith('/alerts')).flush({unreadCount:1,total:1,page:1,items:[alert]});f.detectChanges();
    http.expectNone(r=>r.method==='PUT');expect(f.nativeElement.querySelector('article').classList.contains('unread')).toBe(true);
    const nav=vi.spyOn(TestBed.inject(Router),'navigate').mockResolvedValue(true);const requested=vi.fn();TestBed.inject(AlertsService).reviewRequested.subscribe(requested);
    f.componentInstance.review(alert);const req=http.expectOne(r=>r.url.endsWith('/4/read'));expect(req.request.body).toEqual({version:2});req.flush(null);
    http.expectOne(r=>r.url.endsWith('/unread-count')).flush({unreadCount:0});await f.whenStable();
    expect(nav).toHaveBeenCalledWith(['/budget'],{fragment:'recurring-activity'});expect(requested).toHaveBeenCalledOnce();
  });
  it('shows retry feedback when loading fails',()=>{
    const f=TestBed.createComponent(HouseholdAlerts);f.detectChanges();http.expectOne(r=>r.url.endsWith('/alerts')).flush({}, {status:500,statusText:'Error'});f.detectChanges();expect(f.nativeElement.textContent).toContain('Alerts couldn’t load');
  });
});
