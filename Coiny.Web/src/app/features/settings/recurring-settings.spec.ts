import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RecurringSettings } from './recurring-settings';
describe('Household recurring setting',()=>{
  it('stages toggle changes for the shared save action instead of writing immediately',()=>{
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});const http=TestBed.inject(HttpTestingController);
    const f=TestBed.createComponent(RecurringSettings);f.detectChanges();http.expectOne(r=>r.method==='GET').flush({enabled:false,lastScanDate:null,editVersion:4});f.detectChanges();
    const toggle=f.nativeElement.querySelector('input') as HTMLInputElement;toggle.click();f.detectChanges();
    expect(f.componentInstance.change).toEqual({enabled:true,expectedEditVersion:4});http.expectNone(r=>r.method==='PUT');
    toggle.click();f.detectChanges();expect(f.componentInstance.change).toBeUndefined();http.verify();
  });
});
