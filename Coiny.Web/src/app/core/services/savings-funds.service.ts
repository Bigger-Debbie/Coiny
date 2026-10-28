import { Injectable,inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { SavingsAccount,SavingsFund,SaveFund,FundPreview,FundCandidate } from '../../features/savings/savings.models';
@Injectable({providedIn:'root'})
export class SavingsFundsService {
 private readonly http=inject(HttpClient); private readonly url=environemnt.apiUrl+'/savings-funds';
 accounts(id:number|null=null){return this.http.get<SavingsAccount[]>(this.url,{params:id?{accountId:id}:{}});}
 preview(request:SaveFund){return this.http.post<FundPreview>(this.url+'/preview',request);}
 save(request:SaveFund){return this.http.post<SavingsFund>(this.url,request);}
 move(key:string,request:unknown){return this.http.post<SavingsFund>(`${this.url}/${key}/move`,request);}
 correct(key:string,operation:string,version:number){return this.http.post<SavingsFund>(`${this.url}/${key}/activity/${operation}/correct`,{},{params:{expectedEditVersion:version}});}
 archive(key:string,request:unknown){return this.http.post<void>(`${this.url}/${key}/archive`,request);}
 candidates(key:string,kind:string){return this.http.get<FundCandidate[]>(`${this.url}/${key}/candidates`,{params:{kind}});}
 activity(key:string,request:unknown){return this.http.post<SavingsFund>(`${this.url}/${key}/activity`,request);}
}
