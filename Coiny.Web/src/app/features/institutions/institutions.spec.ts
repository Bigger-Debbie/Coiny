import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NgForm } from '@angular/forms';
import { Institutions } from './institutions';
import { InstitutionService } from '../../core/services/institution.service';
const bank = { id: 1, name: 'Bank', website: 'https://bank.example', isActive: true };
describe('Institutions', () => {
  let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ imports: [Institutions], providers: [provideHttpClient(), provideHttpClientTesting()] }); http=TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  function render() {
    const f=TestBed.createComponent(Institutions);f.detectChanges();
    http.expectOne(r=>r.url.endsWith('/institutions')).flush([bank,{id:2,name:'Empty bank',website:null,isActive:true}]);
    http.expectOne(r=>r.url.endsWith('/accounts')).flush([{id:1,institutionId:1,name:'Checking',accountType:'Checking',isActive:true},{id:2,institutionId:1,name:'Old card',accountType:'CreditCard',isActive:false}]);f.detectChanges();return f;
  }
  it('groups linked and closed accounts into institution cards',()=>{
    const f=render();expect(f.nativeElement.querySelectorAll('.institution-card')).toHaveLength(2);
    const first=f.nativeElement.querySelector('.institution-card');for(const text of ['Checking','Old card','Closed','Linked accounts · 2'])expect(first.textContent).toContain(text);
    expect(f.nativeElement.textContent).toContain('No linked accounts.');expect(first.querySelector('a').getAttribute('rel')).toContain('noopener');
  });
  it('starts compact and expands each institution’s accounts independently',()=>{
    const f=render();const lists=f.nativeElement.querySelectorAll('.linked-accounts');const toggles=f.nativeElement.querySelectorAll('.accounts-toggle');
    expect(lists[0].hidden).toBe(true);expect(lists[1].hidden).toBe(true);expect(toggles[0].getAttribute('aria-expanded')).toBe('false');
    toggles[0].click();f.detectChanges();expect(lists[0].hidden).toBe(false);expect(lists[1].hidden).toBe(true);expect(toggles[0].getAttribute('aria-expanded')).toBe('true');
    toggles[0].click();f.detectChanges();expect(lists[0].hidden).toBe(true);
  });
  it('updates metadata with stale-write protection and notifies the current page',()=>{
    const f=render();const c=f.componentInstance;const changed=vi.fn();TestBed.inject(InstitutionService).changed.subscribe(changed);
    c.edit(bank);c.name='New bank';c.website='';c.save({invalid:false} as NgForm);c.save({invalid:false} as NgForm);
    const req=http.expectOne(r=>r.method==='PUT');expect(req.request.url).toContain('/institutions/1');expect(req.request.body).toEqual({name:'New bank',website:null,expectedName:'Bank',expectedWebsite:'https://bank.example'});
    req.flush({...bank,name:'New bank',website:null});f.detectChanges();expect(changed).toHaveBeenCalledOnce();expect(c.editing()).toBeNull();expect(f.nativeElement.textContent).toContain('New bank');
    expect(f.nativeElement.textContent).toContain('Old card');
  });
  it('blocks invalid URLs and stale retries while retaining the user’s draft',()=>{
    const f=render();const c=f.componentInstance;c.edit(bank);c.website='javascript:alert(1)';c.save({invalid:false} as NgForm);http.expectNone(r=>r.method==='PUT');
    c.website='https://new.example';c.save({invalid:false} as NgForm);http.expectOne(r=>r.method==='PUT').flush({detail:'This institution changed.'},{status:409,statusText:'Conflict'});
    expect(c.stale()).toBe(true);expect(c.website).toBe('https://new.example');c.save({invalid:false} as NgForm);http.expectNone(r=>r.method==='PUT');
  });
});
