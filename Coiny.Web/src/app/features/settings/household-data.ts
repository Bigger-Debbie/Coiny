import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environemnt } from '../../../environments/environment';
import { Modal } from '../../shared/components/modal/modal';
interface Preview { previewId:string; expiresAt:string; backupCreatedAt:string; portable:boolean; backupAccounts:number; backupTransactions:number; currentAccounts:number; currentTransactions:number; }
@Component({selector:'app-household-data',imports:[FormsModule,DatePipe,Modal],templateUrl:'./household-data.html',styleUrl:'./household-data.css'})
export class HouseholdData implements OnInit {
  private readonly http=inject(HttpClient);private readonly destroy=inject(DestroyRef);private readonly url=`${environemnt.apiUrl}/household-data`;
  readonly enabled=signal(false);readonly owner=signal(false);readonly loading=signal(true);readonly busy=signal(false);readonly error=signal('');readonly message=signal('');
  readonly action=signal<'backup'|'restore'|'export'|'safety'|null>(null);readonly preview=signal<Preview|null>(null);readonly safety=signal<{id:string;createdAt:string}[]>([]);
  currentPassword='';backupPassword='';confirmPassword='';passwordProtected=false;confirmReplacement=false;file:File|null=null;safetyId='';private limit=64*1024*1024;
  ngOnInit(){this.http.get<{enabled:boolean;owner:boolean;maximumFileBytes:number}>(`${this.url}/capabilities`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.enabled.set(r.enabled);this.owner.set(r.owner);this.limit=r.maximumFileBytes;this.loading.set(false);if(r.enabled&&r.owner)this.loadSafety();},error:r=>{this.loading.set(false);if(r.status!==409)this.error.set('Household data tools couldn’t load. Reload Settings to try again.');}});}
  loadSafety(){this.http.get<{id:string;createdAt:string}[]>(`${this.url}/safety`).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>this.safety.set(r),error:()=>this.error.set('Safety backup history couldn’t load. Reload Settings before restoring.')});}
  open(action:'backup'|'restore'|'export'|'safety',id=''){if(this.busy())return;this.reset();this.error.set('');this.message.set('');this.action.set(action);this.safetyId=id;}
  close(){if(!this.busy()){this.action.set(null);this.reset();this.error.set('');}}
  private reset(){this.currentPassword=this.backupPassword=this.confirmPassword='';this.passwordProtected=false;this.preview.set(null);this.confirmReplacement=false;this.file=null;this.safetyId='';}
  chooseFile(event:Event){this.file=(event.target as HTMLInputElement).files?.[0]??null;this.preview.set(null);this.error.set('');}
  async fail(response:any){this.busy.set(false);let body=response.error;if(body instanceof Blob){try{body=JSON.parse(await body.text());}catch{body=null;}}this.error.set(response.status===429?'Too many attempts. Please wait a minute.':body?.message||'This request couldn’t finish. Check the current household state before retrying a restore.');}
  download(){
    if(this.busy()||!this.currentPassword)return;const action=this.action();
    if(action==='backup'&&this.passwordProtected&&(this.backupPassword.length<12||this.backupPassword!==this.confirmPassword)){this.error.set('Use a backup password of at least 12 characters and enter it twice.');return;}
    this.busy.set(true);this.error.set('');const endpoint=action==='safety'?`safety/${this.safetyId}`:action;
    const body=action==='backup'?{currentPassword:this.currentPassword,passwordProtected:this.passwordProtected,backupPassword:this.passwordProtected?this.backupPassword:null}:{currentPassword:this.currentPassword};
    this.http.post(`${this.url}/${endpoint}`,body,{responseType:'blob'}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:blob=>{const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download=action==='export'?'coiny-household-export.zip':action==='safety'?'coiny-before-restore.coiny':'coiny-household-backup.coiny';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);this.busy.set(false);this.close();this.message.set('Your download is ready. Keep it somewhere private.');},error:r=>void this.fail(r)});
  }
  review(){if(this.busy()||!this.currentPassword||!this.file)return;if(this.file.size>this.limit){this.error.set('Choose a Coiny backup smaller than 64 MiB.');return;}const form=new FormData();form.append('file',this.file);form.append('currentPassword',this.currentPassword);if(this.backupPassword)form.append('backupPassword',this.backupPassword);this.busy.set(true);this.error.set('');this.http.post<Preview>(`${this.url}/restore/preview`,form).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.busy.set(false);this.preview.set(r);this.currentPassword=this.backupPassword='';this.file=null;},error:r=>void this.fail(r)});}
  restore(){const preview=this.preview();if(!preview||this.busy()||!this.currentPassword||!this.confirmReplacement)return;this.busy.set(true);this.error.set('');this.http.post(`${this.url}/restore/confirm`,{previewId:preview.previewId,currentPassword:this.currentPassword,confirmReplacement:true}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.busy.set(false);this.close();this.message.set('Household data restored. A safety backup is available below. Refresh other open Coiny tabs.');this.loadSafety();},error:r=>{if(r.status===409){this.preview.set(null);this.confirmReplacement=false;this.currentPassword='';}void this.fail(r);}});}
}
