import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule, NgForm } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { environemnt } from '../../../environments/environment';
interface Setup { setupToken: string; sharedKey: string; qrCodeDataUrl: string; }
@Component({selector:'app-authenticator-mfa',imports:[FormsModule],templateUrl:'./authenticator-mfa.html',
  styleUrls:['./account-security.css','./authenticator-mfa.css']})
export class AuthenticatorMfa {
  private readonly http=inject(HttpClient); private readonly auth=inject(AuthService);
  private readonly destroy=inject(DestroyRef); private readonly toast=inject(ToastService);
  private readonly url=environemnt.apiUrl+'/auth/mfa';
  readonly changed=output<void>(); readonly status=signal<{enabled:boolean;recoveryCodesRemaining:number}|null>(null);
  readonly setup=signal<Setup|null>(null); readonly codes=signal<string[]>([]);
  readonly busy=signal(false); readonly error=signal('');
  password=''; code=''; recoveryCode=false;
  constructor(){this.load();}
  load(){this.error.set('');this.http.get<{enabled:boolean;recoveryCodesRemaining:number}>(this.url).pipe(takeUntilDestroyed(this.destroy)).subscribe({
    next:status=>this.status.set(status),error:()=>this.error.set('Authenticator status couldn’t load. Please try again.')});}
  begin(form:NgForm){
    if(form.invalid||this.busy())return;
    this.busy.set(true);this.error.set('');
    this.http.post<Setup>(this.url+'/setup',{currentPassword:this.password}).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next:setup=>{this.setup.set(setup);this.password='';this.code='';this.busy.set(false);form.resetForm();},error:e=>this.failed(e)});
  }
  cancel(){this.setup.set(null);this.password='';this.code='';this.error.set('');}
  enable(form:NgForm){
    if(form.invalid||this.busy()||!this.setup())return;
    this.save('/enable',{setupToken:this.setup()!.setupToken,code:this.code},true,form);
  }
  manage(form:NgForm,disable=false){
    if(form.invalid||this.busy())return;
    this.save(disable?'/disable':'/recovery-codes',{currentPassword:this.password,code:this.code,recoveryCode:this.recoveryCode},!disable,form);
  }
  private save(path:string,body:object,enabled:boolean,form:NgForm){
    this.busy.set(true);this.error.set('');this.codes.set([]);
    this.http.post<{token:string;recoveryCodes:string[]}>(this.url+path,body).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next:response=>{
        this.auth.renewSession(response.token);this.codes.set(response.recoveryCodes);this.setup.set(null);
        this.status.set({enabled,recoveryCodesRemaining:response.recoveryCodes.length});
        this.password=this.code='';this.recoveryCode=false;form.resetForm();this.busy.set(false);this.changed.emit();
        this.toast.success(enabled?'Authenticator security updated. Save your backup codes.':'Authenticator sign-in disabled.');
      },error:e=>this.failed(e)});
  }
  private failed(error:any){this.busy.set(false);this.code='';this.error.set(error.status===429?'Too many attempts. Please wait before trying again.':error.error?.message||'This change couldn’t be saved. Check your security settings before retrying.');}
  download(){
    const url=URL.createObjectURL(new Blob(['Coiny MFA backup codes\n\nEach code replaces your authenticator code once at sign-in. Your password is still required.\n\n'+this.codes().join('\n')+'\n'],{type:'text/plain'}));
    const link=document.createElement('a');link.href=url;link.download='coiny-mfa-backup-codes.txt';link.click();setTimeout(()=>URL.revokeObjectURL(url),0);
  }
}
