import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { Account } from "../../shared/models/account/account";

@Injectable({
    providedIn: 'root'
})
export class AccountService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/accounts`;

    getAccounts(): Observable<Account[]> {
        return this.http.get<Account[]>(this.apiUrl);
    }
}