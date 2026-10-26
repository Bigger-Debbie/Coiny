import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AvailableCash } from './available-cash';
const result = { balanceDate:'2026-10-26',endDate:'2026-10-29',cashBalance:1615.46,cardBalance:1033.15,expectedIncome:0,remainingAllocations:741.26,expectedRemaining:-158.95,accountIds:[1,3],accounts:[
  {id:1,name:'Checking',accountType:'Checking',balance:315.45,isActive:true},
  {id:2,name:'Emergency',accountType:'Savings',balance:10000,isActive:true},
  {id:3,name:'Card',accountType:'CreditCard',balance:1033.15,isActive:true},
] };
describe('AvailableCash',()=>{
  beforeAll(()=>{
    Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,value(){}});
    Object.defineProperty(HTMLDialogElement.prototype,'close',{configurable:true,value(){}});
  });
  afterAll(()=>{Reflect.deleteProperty(HTMLDialogElement.prototype,'showModal'); Reflect.deleteProperty(HTMLDialogElement.prototype,'close');});
  let http:HttpTestingController;
  beforeEach(()=>{ TestBed.configureTestingModule({imports:[AvailableCash],providers:[provideHttpClient(),provideHttpClientTesting()]}); http=TestBed.inject(HttpTestingController); });
  afterEach(()=>http.verify());
  function render() { const f=TestBed.createComponent(AvailableCash); f.componentRef.setInput('asOf','2026-10-16'); f.detectChanges(); return f; }
  it('shows the balance-based breakdown and applies selected accounts without changing ledger entries',()=>{
    const f=render(); const req=http.expectOne(r=>r.url.endsWith('/available-cash'));
    expect(req.request.body).toEqual({asOf:'2026-10-16',accountIds:null}); req.flush(result); f.detectChanges();
    expect(f.nativeElement.querySelector('strong').textContent).toContain('-$158.95');
    expect(f.nativeElement.querySelector('app-modal')).toBeNull();
    f.componentInstance.openDetails(); f.detectChanges();
    expect(f.nativeElement.querySelector('app-modal').textContent).toContain('savings goals and investment contributions');
    const boxes=f.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    expect(boxes[1].checked).toBe(false); boxes[1].click(); f.detectChanges();
    const saved=vi.fn(); f.componentInstance.accountsSaved.subscribe(saved);
    f.nativeElement.querySelector('.apply-accounts').click();
    const apply=http.expectOne(r=>r.url.endsWith('/available-cash')); expect(apply.request.body.accountIds).toEqual([1,3,2]);
    apply.flush({...result,accountIds:[1,2,3],expectedRemaining:9841.05}); f.detectChanges(); expect(saved).toHaveBeenCalledWith([1,2,3]); expect(f.nativeElement.querySelector('app-modal')).toBeNull();
    // Updating the remembered input after apply does not trigger a duplicate calculation.
    f.componentRef.setInput('accountIds',[1,2,3]); f.detectChanges(); http.expectNone(r=>r.url.endsWith('/available-cash'));
    f.componentRef.setInput('refreshKey',1); f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/available-cash')).flush({...result,accountIds:[1,2,3],expectedRemaining:9841.05});
  });
  it('requires cash selection and recovers from invalid saved accounts with reset',()=>{
    const f=render(); http.expectOne(r=>r.url.endsWith('/available-cash')).flush({detail:'Reset accounts if a saved account was removed.'},{status:400,statusText:'Bad Request'}); f.detectChanges();
    expect(f.nativeElement.querySelector('[role="alert"]').textContent).toContain('Reset accounts');
    const reset=[...f.nativeElement.querySelectorAll('button')].find((b:any)=>b.textContent.includes('Reset')) as HTMLButtonElement; reset.click();
    const req=http.expectOne(r=>r.url.endsWith('/available-cash')); expect(req.request.body.accountIds).toBeNull(); req.flush({...result,accountIds:[3],expectedRemaining:null}); f.detectChanges();
    f.componentInstance.openDetails(); f.detectChanges();
    expect(f.nativeElement.querySelector('.apply-accounts').disabled).toBe(true);
    expect(f.nativeElement.textContent).toContain('Select at least one cash account');
  });
});
