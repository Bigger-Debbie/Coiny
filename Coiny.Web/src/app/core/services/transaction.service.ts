import { TransactionEditDetails, UpdateTransactionRequest } from "../../shared/models/transactions/transaction-management";
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

    marketBalance(accountId: number, date: string, excludingId?: number) {
        return this.http.get<{ balance: number }>(`${this.apiUrl}/market-adjustments/balance`, {
            params: { accountId, date, ...(excludingId ? { excludingId } : {}) },
        });
    }
    createMarketAdjustment(request: { accountId: number; date: string; updatedBalance: number; expectedBalance: number; notes: string | null; isCleared: boolean }) {
        return this.http.post<Transaction>(`${this.apiUrl}/market-adjustments`, request);
    }
    getDetails(id: number) { return this.http.get<TransactionEditDetails>(`${this.apiUrl}/${id}`); }
    update(id: number, request: UpdateTransactionRequest) { return this.http.put<TransactionEditDetails>(`${this.apiUrl}/${id}`, request); }
    delete(id: number, expectedEditVersion: number, pairedVersion: number | null) {
        const params: Record<string, number> = { expectedEditVersion };
        if (pairedVersion !== null) params['expectedPairedEditVersion'] = pairedVersion;
        return this.http.delete<void>(`${this.apiUrl}/${id}`, { params });
    }
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
