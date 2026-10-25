import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, Subscription, timer } from 'rxjs';
import { environemnt } from '../../../environments/environment';
export interface HouseholdAlert {id:number;merchant:string;frequency:string;typicalAmount:number;nextExpected:string;updatedAt:string;unread:boolean;version:number;}
export interface AlertsPage {unreadCount:number;total:number;page:number;items:HouseholdAlert[];}
@Injectable({providedIn:'root'})
export class AlertsService {
  private readonly http=inject(HttpClient);private readonly url=`${environemnt.apiUrl}/alerts`;
  private poll?:Subscription;private countRequest?:Subscription;
  readonly unread=signal(0);readonly reviewRequested=new Subject<void>();
  start() {this.stop();this.poll=timer(0,60000).subscribe(()=>this.refresh());}
  stop() {this.poll?.unsubscribe();this.countRequest?.unsubscribe();this.unread.set(0);}
  refresh() {this.countRequest?.unsubscribe();this.countRequest=this.http.get<{unreadCount:number}>(`${this.url}/unread-count`).subscribe({next:r=>this.unread.set(r.unreadCount),error:()=>{}});}
  list(page=1) {return this.http.get<AlertsPage>(this.url,{params:{page}});}
  read(alert:HouseholdAlert) {return this.http.put<void>(`${this.url}/${alert.id}/read`,{version:alert.version});}
  review() {this.reviewRequested.next();}
}
