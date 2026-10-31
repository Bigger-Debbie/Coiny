import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController,provideHttpClientTesting} from '@angular/common/http/testing';
import {AuthenticatorMfa} from './authenticator-mfa';
import {AuthService} from '../../core/services/auth.service';

describe('Authenticator MFA setup',()=>{
  const renewSession=vi.fn();
  function setup(enabled=false){
    TestBed.configureTestingModule({imports:[AuthenticatorMfa],providers:[provideHttpClient(),provideHttpClientTesting(),{provide:AuthService,useValue:{renewSession}}]});
    const fixture=TestBed.createComponent(AuthenticatorMfa);const http=TestBed.inject(HttpTestingController);fixture.detectChanges();
    http.expectOne(r=>r.url.endsWith('/auth/mfa')).flush({enabled,recoveryCodesRemaining:enabled?10:0});fixture.detectChanges();return{fixture,http,c:fixture.componentInstance};
  }
  beforeEach(()=>renewSession.mockReset());
  it('shows the locally generated QR code and manual key, and enables only after confirmation',()=>{
    const{fixture,http,c}=setup();const form={invalid:false,resetForm:vi.fn()} as any;
    c.password='password';c.begin(form);http.expectOne(r=>r.url.endsWith('/mfa/setup')).flush({setupToken:'protected',sharedKey:'TESTKEY',qrCodeDataUrl:'data:image/png;base64,iVBORw0KGgo='});fixture.detectChanges();
    expect(c.status()?.enabled).toBe(false);expect(c.password).toBe('');expect(fixture.nativeElement.querySelector('.mfa-qr').getAttribute('src')).toContain('data:image/png');expect(fixture.nativeElement.textContent).toContain('TESTKEY');
    c.code='123456';c.enable(form);const request=http.expectOne(r=>r.url.endsWith('/mfa/enable'));expect(request.request.body).toEqual({setupToken:'protected',code:'123456'});
    request.flush({token:'renewed',recoveryCodes:['BACKUP']});fixture.detectChanges();
    expect(renewSession).toHaveBeenCalledWith('renewed');expect(c.setup()).toBeNull();expect(c.status()?.enabled).toBe(true);expect(c.codes()).toEqual(['BACKUP']);expect(c.code).toBe('');http.verify();
  });
  it('clears setup secrets when canceled and keeps the current session after a rejected code',()=>{
    const{http,c}=setup();const form={invalid:false,resetForm:vi.fn()} as any;c.password='password';c.begin(form);
    http.expectOne(r=>r.url.endsWith('/mfa/setup')).flush({setupToken:'protected',sharedKey:'KEY',qrCodeDataUrl:'data:image/png;base64,test'});
    c.code='123456';c.enable(form);http.expectOne(r=>r.url.endsWith('/mfa/enable')).flush({message:'Invalid code'},{status:400,statusText:'Bad request'});
    expect(c.error()).toContain('Invalid code');expect(renewSession).not.toHaveBeenCalled();c.cancel();expect(c.setup()).toBeNull();expect(c.code).toBe('');http.verify();
  });
  it('requires a password and factor to disable, renewing the current browser session',()=>{
    const{http,c}=setup(true);c.password='password';c.code='BACKUP';c.recoveryCode=true;c.manage({invalid:false,resetForm:vi.fn()} as any,true);
    const request=http.expectOne(r=>r.url.endsWith('/mfa/disable'));expect(request.request.body).toEqual({currentPassword:'password',code:'BACKUP',recoveryCode:true});request.flush({token:'renewed',recoveryCodes:[]});
    expect(c.status()?.enabled).toBe(false);expect(c.password).toBe('');expect(renewSession).toHaveBeenCalledWith('renewed');http.verify();
  });
});
