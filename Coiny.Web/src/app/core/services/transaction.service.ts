import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { CreateTransactionRequest } from "../../shared/models/transactions/create-transaction-request";
import { Transaction } from "../../shared/models/transactions/transaction";

@Injectable({
    providedIn: 'root'
})
export class TransactionService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/transactions`;

    getTransactions(): Observable<Transaction[]> {
        return this.http.get<Transaction[]>(this.apiUrl);
    }

    CreateTransaction(request: CreateTransactionRequest): Observable<Transaction> {
        return this.http.post<Transaction>(this.apiUrl, request);
    }
}