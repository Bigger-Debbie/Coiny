import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { environemnt } from '../../../environments/environment';

interface Setting {enabled:boolean;lastScanDate:string|null;editVersion:number;}
@Component({selector:'app-recurring-settings',imports:[DatePipe],template:`
  <h3>Recurring detection · Household</h3>
  @if(loading()){<p role="status">Loading household setting…</p>}
  @if(setting(); as s) {
    <label><span>Scan daily for recurring expenses</span><input type="checkbox" role="switch" [checked]="enabled" (change)="enabled=$any($event.target).checked" aria-describedby="recurring-auto-help" /></label>
    <p id="recurring-auto-help" class="hint">This setting applies to everyone in your household. When enabled, Coiny checks within 15 minutes while the API is running, then once per day. Manual scans remain available.</p>
    <small>{{ s.lastScanDate ? 'Last completed scan: ' + (s.lastScanDate | date:'MMM d, yyyy':'UTC') : 'No automatic scans completed yet.' }}</small>
  }
  @if(error()) {<p role="alert">{{ error() }}</p><button type="button" class="button-outline" (click)="load()">Reload setting</button>}
  `,styles:`:host {display:block;border-top:1px solid var(--color-border);margin-top:1.5rem;padding-top:1.25rem;} h3 {color:var(--color-primary);margin:0 0 1rem;font-size:1.2rem;font-weight:400;text-transform:none;letter-spacing:normal;} label {display:flex;align-items:center;justify-content:space-between;gap:1rem;} p,small {font-size:.85rem;line-height:1.6;color:var(--color-text-muted);} [role=alert] {color:var(--color-danger);} input {appearance:none;flex-shrink:0;width:2.75rem;height:1.5rem;padding:2px;border:0;border-radius:2rem;background:var(--color-border);cursor:pointer;} input::before {content:'';display:block;width:1.25rem;height:1.25rem;border-radius:50%;background:white;} input:checked {background:var(--color-primary);} input:checked::before {transform:translateX(1.25rem);}`})
export class RecurringSettings implements OnInit {
  private readonly http=inject(HttpClient);private readonly destroy=inject(DestroyRef);
  private readonly url=`${environemnt.apiUrl}/recurring-expenses/settings`;
  readonly setting=signal<Setting|null>(null);readonly loading=signal(false);readonly error=signal('');
  enabled=false;
  get change() {const s=this.setting();return s && s.enabled!==this.enabled ? {enabled:this.enabled,expectedEditVersion:s.editVersion}:undefined;}
  ngOnInit(){this.load();}
  load(){this.loading.set(true);this.error.set('');this.http.get<Setting>(this.url).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:s=>{this.setting.set(s);this.enabled=s.enabled;this.loading.set(false);},error:()=>{this.loading.set(false);this.error.set('Household recurring settings couldn’t load.');}});}
}
