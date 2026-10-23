import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { BudgetActuals } from '../../shared/models/budget/budget-actuals';
import { BillPaymentState, BillTransactionCandidate, BudgetEvent, BudgetIncomePeriod, BudgetDraftRequest, BudgetMonth, BudgetPreview, BudgetRevision, BudgetSetup } from '../../shared/models/budget/budget';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/budgets`;
  actuals(month: string, view: string, type: string, page: number, allocationKey?: string) {
    return this.http.get<BudgetActuals>(`${this.url}/actuals`, { params: { month, view, page, ...(type ? { type } : {}), ...(allocationKey ? { allocationKey } : {}) } });
  }
  periodActuals(asOf: string, view: string, type: string, page: number, allocationKey?: string) {
    return this.http.get<BudgetActuals>(`${this.url}/actuals/period`, { params: { asOf, view, page, ...(type ? { type } : {}), ...(allocationKey ? { allocationKey } : {}) } });
  }
  saveCategoryDefault(categoryId: number, month: string, allocationKey: string | null, expectedEditVersion: number) {
    return this.http.put<void>(`${this.url}/actuals/categories/${categoryId}`, { month, allocationKey, expectedEditVersion });
  }
  assignTransaction(id: number, month: string, allocationKey: string | null, useCategoryDefault: boolean, expectedEditVersion: number, expectedPairedEditVersion: number | null) {
    return this.http.put<void>(`${this.url}/actuals/transactions/${id}`, { month, allocationKey, useCategoryDefault, expectedEditVersion, expectedPairedEditVersion });
  }
  saveBillPayment(bill: BudgetEvent, request: BillPaymentState) { return this.http.put<BillPaymentState>(`${this.url}/bills/${bill.sourceKey}/${bill.date}/payment`, request); }
  billTransactions(bill: BudgetEvent, search: string) { return this.http.get<BillTransactionCandidate[]>(`${this.url}/bills/${bill.sourceKey}/${bill.date}/transactions`, { params: { search } }); }
  setup() { return this.http.get<BudgetSetup>(`${this.url}/setup`); }
  start(effectiveFromMonth: string) { return this.http.post<BudgetRevision>(`${this.url}/draft`, { effectiveFromMonth }); }
  save(id: number, request: BudgetDraftRequest) { return this.http.put<BudgetRevision>(`${this.url}/draft/${id}`, request); }
  preview(id: number, expectedEditVersion: number, month: string) {
    return this.http.get<BudgetPreview>(`${this.url}/draft/${id}/preview`, { params: { expectedEditVersion, month } });
  }
  publish(id: number, expectedEditVersion: number) { return this.http.post<BudgetRevision>(`${this.url}/draft/${id}/publish`, { expectedEditVersion }); }
  incomePeriod(sourceKey: string | null = null, asOf?: string) {
    return this.http.get<BudgetIncomePeriod>(`${this.url}/income-period`, { params: { ...(sourceKey ? { sourceKey } : {}), ...(asOf ? { asOf } : {}) } });
  }
  month(month: string) { return this.http.get<BudgetMonth>(`${this.url}/month`, { params: { month } }); }
}
