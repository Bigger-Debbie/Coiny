import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { CreateTransactionRequest } from "../../shared/models/transactions/create-transaction-request";
import { Transaction } from "../../shared/models/transactions/transaction";
import { TransactionPage, TransactionQuery } from "../../shared/models/transactions/transaction-page";
import { CategoryBreakdown, CategoryBreakdownQuery } from "../../shared/models/transactions/category-breakdown";

@Injectable({
    providedIn: 'root'
})
export class TransactionService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/transactions`;

    getTransactions(): Observable<Transaction[]> {
        return this.http.get<Transaction[]>(this.apiUrl);
    }
    getPage(query: TransactionQuery): Observable<TransactionPage> {
        const params: Record<string, string | number> = {};
        Object.entries(query).forEach(([key, value]) => {
            if (value !== undefined && value !== '') params[key] = value;
        });
        return this.http.get<TransactionPage>(`${this.apiUrl}/paged`, { params });
    }

    getCategoryBreakdown(query: CategoryBreakdownQuery): Observable<CategoryBreakdown> {
        const params: Record<string, string | number> = {
            startDate: query.startDate, endDate: query.endDate, categoryType: query.categoryType,
        };
        if (query.accountId !== undefined) params['accountId'] = query.accountId;
        return this.http.get<CategoryBreakdown>(`${this.apiUrl}/category-breakdown`, { params });
    }

    CreateTransaction(request: CreateTransactionRequest): Observable<Transaction> {
        return this.http.post<Transaction>(this.apiUrl, request);
    }
}
