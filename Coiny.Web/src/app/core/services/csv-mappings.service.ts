import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { CsvMapping, BankBalanceOrder } from '../../features/transactions/import-transactions/csv-mapping';
import { CsvDelimiter } from '../../features/transactions/import-transactions/csv-parser';
export interface CsvMappingSettings {headers:string[];mapping:CsvMapping;encoding:string;delimiter:CsvDelimiter;headerLine:number;balanceOrder:BankBalanceOrder|null;}
export interface SavedCsvMapping {id:number;name:string;accountType:string;institutionId:number|null;settings:CsvMappingSettings;editVersion:number;}
export interface SaveCsvMapping {name:string;accountType:string;institutionId:number|null;settings:CsvMappingSettings;expectedEditVersion:number;}
@Injectable({providedIn:'root'})
export class CsvMappingsService {
 private readonly http=inject(HttpClient);private readonly url=`${environemnt.apiUrl}/csv-mappings`;
 list(){return this.http.get<SavedCsvMapping[]>(this.url);}
 save(id:number|null,input:SaveCsvMapping){return id===null?this.http.post<SavedCsvMapping>(this.url,input):this.http.put<SavedCsvMapping>(`${this.url}/${id}`,input);}
 delete(profile:SavedCsvMapping){return this.http.delete<void>(`${this.url}/${profile.id}`,{params:{expectedEditVersion:profile.editVersion}});}
}
