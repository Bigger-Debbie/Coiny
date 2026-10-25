import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MerchantRules } from './merchant-rules';
import { MerchantRuleEditor } from './merchant-rule-editor';
import { MerchantRule } from '../../core/services/merchant-rules.service';
const rule:MerchantRule={id:1,matchType:'MerchantExact',pattern:'Netflix',categoryId:1,categoryName:'Subscriptions',categoryType:'Expense',categoryActive:true,isActive:true,editVersion:2};
describe('Merchant rules management',()=>{
 let http:HttpTestingController;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());
 it('loads household rules and toggles them using their current version',()=>{
  const f=TestBed.createComponent(MerchantRules);f.detectChanges();http.expectOne(r=>r.method==='GET').flush([rule]);f.detectChanges();
  expect(f.nativeElement.textContent).toContain('Netflix');f.componentInstance.toggle(rule);const req=http.expectOne(r=>r.method==='PUT');expect(req.request.body).toMatchObject({isActive:false,expectedEditVersion:2});req.flush({...rule,isActive:false,editVersion:3});http.expectOne(r=>r.method==='GET').flush([{...rule,isActive:false,editVersion:3}]);f.detectChanges();expect(f.nativeElement.textContent).toContain('Disabled');
 });
 it('previews matching examples and saves an explicitly reviewed merchant/category pair',()=>{
  const f=TestBed.createComponent(MerchantRuleEditor);f.componentRef.setInput('preset',{pattern:'Netflix',categoryId:1});f.detectChanges();http.expectOne(r=>r.url.endsWith('/categories')).flush([{id:1,name:'Subscriptions',categoryType:'Expense',isActive:true},{id:2,name:'Transfers',categoryType:'Transfer',isActive:true}]);f.detectChanges();
  expect(f.componentInstance.categories()).toHaveLength(1);f.componentInstance.preview();http.expectOne(r=>r.url.endsWith('/preview')).flush({transactionsChecked:20,examples:[]});f.detectChanges();expect(f.nativeElement.textContent).toContain('No matches');http.expectNone(r=>r.method==='POST' && !r.url.endsWith('/preview'));
  f.componentInstance.save();const save=http.expectOne(r=>r.url.endsWith('/merchant-rules'));expect(save.request.body).toEqual({matchType:'MerchantExact',pattern:'Netflix',categoryId:1,isActive:true,expectedEditVersion:0});save.flush(rule);
 });
});
