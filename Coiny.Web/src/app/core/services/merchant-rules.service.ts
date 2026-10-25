import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subject, tap } from 'rxjs';
import { environemnt } from '../../../environments/environment';
export type MatchType='MerchantExact'|'DescriptionContains';
export interface MerchantRule {id:number;matchType:MatchType;pattern:string;categoryId:number;categoryName:string;categoryType:'Income'|'Expense';categoryActive:boolean;isActive:boolean;editVersion:number;}
export interface MerchantRuleInput {matchType:MatchType;pattern:string;categoryId:number;isActive:boolean;expectedEditVersion:number;}
export interface RulePreview {transactionsChecked:number;examples:{id:number;date:string;merchant:string;description:string;amount:number;accountName:string}[];}
@Injectable({providedIn:'root'})
export class MerchantRulesService {
  private readonly http=inject(HttpClient);private readonly url=`${environemnt.apiUrl}/merchant-rules`;
  readonly changed=new Subject<void>();
  list(){return this.http.get<MerchantRule[]>(this.url);}
  save(id:number|null,input:MerchantRuleInput){return (id===null?this.http.post<MerchantRule>(this.url,input):this.http.put<MerchantRule>(`${this.url}/${id}`,input)).pipe(tap(()=>this.changed.next()));}
  delete(rule:MerchantRule){return this.http.delete<void>(`${this.url}/${rule.id}`,{params:{expectedEditVersion:rule.editVersion}}).pipe(tap(()=>this.changed.next()));}
  preview(input:MerchantRuleInput){return this.http.post<RulePreview>(`${this.url}/preview`,input);}
}
