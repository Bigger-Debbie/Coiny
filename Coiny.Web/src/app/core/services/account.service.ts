import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { Account } from "../../shared/models/account/account";
import { TrendResult } from "../../shared/models/trends/trend-result";
import { CreateAccountRequest } from "../../shared/models/account/create-account-request";

@Injectable({
    providedIn: 'root'
})
export class AccountService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/accounts`;

    setLoanSubtype(id: number, loanSubtype: string) { return this.http.put<void>(`${this.apiUrl}/${id}/loan-subtype`, { loanSubtype }); }
    getAccounts(): Observable<Account[]> {
        return this.http.get<Account[]>(this.apiUrl);
    }
    getTrend(accountId: number, startDate: string, endDate: string): Observable<TrendResult> {
        return this.http.get<TrendResult>(`${this.apiUrl}/${accountId}/trend`, { params: { startDate, endDate } });
    }
    createAccount(request: CreateAccountRequest): Observable<Account> {
        return this.http.post<Account>(this.apiUrl, request);
    }
}
