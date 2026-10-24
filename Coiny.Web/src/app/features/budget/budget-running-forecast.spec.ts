import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BudgetRunningForecast } from './budget-running-forecast';
import { BudgetForecast } from '../../shared/models/budget/budget-actuals';

const outlook: BudgetForecast={ startDate:'2026-10-16',endDate:'2027-01-21',throughDate:'2026-10-23',months:3,expectedIncomeAwaitingTransaction:0,plannedSavingsContributions:1000,forecastSavingsContributions:1000,plannedInvestmentContributions:500,forecastInvestmentContributions:500,additionalSavings:0,fundingShortfall:200,potentialSavings:800,shortfallBeyondSavings:0,
  points:[{ date:'2026-10-15',planned:0,actual:0,forecast:null },{ date:'2026-10-23',planned:100,actual:-20,forecast:-20 },{ date:'2027-01-21',planned:500,actual:null,forecast:-200 }],
  periods:[{ startDate:'2026-10-16',endDate:'2026-10-29',plannedIncome:1000,plannedAllocations:500,plannedRemainder:500,forecastIncome:1000,forecastAllocations:1200,forecastRemainder:-200,cumulativePlanned:500,cumulativeForecast:-200 }] };
describe('Running budget forecast',()=>{
  let http: HttpTestingController;
  beforeEach(()=>{ TestBed.configureTestingModule({ imports:[BudgetRunningForecast],providers:[provideHttpClient(),provideHttpClientTesting()] }); http=TestBed.inject(HttpTestingController); });
  afterEach(()=>http.verify());
  function render() { const f=TestBed.createComponent(BudgetRunningForecast); f.componentRef.setInput('asOf','2026-10-23'); f.detectChanges(); return f; }
  it('loads three months by default and displays signed remainder totals and the period breakdown',()=>{
    const f=render(); const request=http.expectOne(r=>r.url.endsWith('/forecast'));
    expect(request.request.params.get('months')).toBe('3'); expect(request.request.params.get('asOf')).toBe('2026-10-23');
    request.flush(outlook); f.detectChanges();
    expect(f.nativeElement.querySelector('.shortfall').textContent).toContain('$200.00');
    expect(f.nativeElement.querySelector('.savings-total').textContent).toContain('$800.00');
    expect(f.nativeElement.querySelector('.remainder-comparison').textContent).toContain('-$200.00');
    expect(f.nativeElement.textContent).toContain('Starts at $0'); expect(f.nativeElement.textContent).toContain('Jan 21, 2027');
    expect(f.nativeElement.querySelector('.period-scroll tbody').textContent).toContain('-$200.00');
    expect(f.nativeElement.querySelector('[aria-label="Chart budget areas"]')).toBeNull();
    expect(f.nativeElement.querySelector('.forecast-line')).toBeTruthy();
  });
  it('cancels obsolete requests when changing horizons and retains retry behavior',()=>{
    const f=render(); const old=http.expectOne(r=>r.url.endsWith('/forecast'));
    f.componentInstance.select(12); expect(old.cancelled).toBe(true);
    const year=http.expectOne(r=>r.url.endsWith('/forecast')); expect(year.request.params.get('months')).toBe('12');
    year.flush({ detail:'Income schedule does not cover this range.' },{ status:400,statusText:'Bad request' }); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="alert"]').textContent).toContain('Income schedule');
    f.componentInstance.select(0); const current=http.expectOne(r=>r.url.endsWith('/forecast')); expect(current.request.params.get('months')).toBe('0');
    current.flush(outlook); f.detectChanges(); expect(f.componentInstance.error()).toBe('');
  });
  it('explains unrecorded income and refreshes when the selected period or refresh key changes',()=>{
    const f=render(); http.expectOne(r=>r.url.endsWith('/forecast')).flush({ ...outlook,expectedIncomeAwaitingTransaction:1000 }); f.detectChanges();
    expect(f.nativeElement.querySelector('.notice').textContent).toContain('$1,000.00');
    f.componentRef.setInput('asOf','2026-11-13'); f.detectChanges();
    const changed=http.expectOne(r=>r.url.endsWith('/forecast')); expect(changed.request.params.get('asOf')).toBe('2026-11-13'); changed.flush(outlook);
    f.componentRef.setInput('refreshKey',1); f.detectChanges(); http.expectOne(r=>r.url.endsWith('/forecast')).flush(outlook);
  });
  it('shows contributions plus additional remainder as potential savings and keeps investments separate',()=>{
    const f=render(); http.expectOne(r=>r.url.endsWith('/forecast')).flush({ ...outlook,plannedSavingsContributions:3000,forecastSavingsContributions:3000,additionalSavings:800,fundingShortfall:0,potentialSavings:3800 }); f.detectChanges();
    expect(f.nativeElement.querySelector('.savings-total strong').textContent).toContain('$3,800.00');
    expect(f.nativeElement.querySelector('.savings-summary').textContent).toContain('Additional forecast remainder');
    expect(f.nativeElement.querySelector('.savings-summary').textContent).toContain('Investment contributions');
    expect(f.nativeElement.querySelector('.shortfall')).toBeNull();
    f.componentInstance.select(6); http.expectOne(r=>r.url.endsWith('/forecast')).flush({ ...outlook,potentialSavings:1700 }); f.detectChanges();
    expect(f.nativeElement.querySelector('.savings-total strong').textContent).toContain('$1,700.00');
  });
  it('shows a funding gap when the shortfall exceeds contributions without displaying negative savings',()=>{
    const f=render(); http.expectOne(r=>r.url.endsWith('/forecast')).flush({ ...outlook,fundingShortfall:1500,potentialSavings:0,shortfallBeyondSavings:500 }); f.detectChanges();
    expect(f.nativeElement.querySelector('.savings-total strong').textContent).toBe('$0.00');
    expect(f.nativeElement.querySelector('.shortfall .negative').textContent).toContain('$1,500.00');
    expect(f.nativeElement.querySelector('.funding-gap').textContent).toContain('$500.00');
  });
});
