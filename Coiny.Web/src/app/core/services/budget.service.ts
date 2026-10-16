import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { BudgetDraftRequest, BudgetMonth, BudgetPreview, BudgetRevision, BudgetSetup } from '../../shared/models/budget/budget';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/budgets`;
  setup() { return this.http.get<BudgetSetup>(`${this.url}/setup`); }
  start(effectiveFromMonth: string) { return this.http.post<BudgetRevision>(`${this.url}/draft`, { effectiveFromMonth }); }
  save(id: number, request: BudgetDraftRequest) { return this.http.put<BudgetRevision>(`${this.url}/draft/${id}`, request); }
  preview(id: number, expectedEditVersion: number, month: string) {
    return this.http.get<BudgetPreview>(`${this.url}/draft/${id}/preview`, { params: { expectedEditVersion, month } });
  }
  publish(id: number, expectedEditVersion: number) { return this.http.post<BudgetRevision>(`${this.url}/draft/${id}/publish`, { expectedEditVersion }); }
  month(month: string) { return this.http.get<BudgetMonth>(`${this.url}/month`, { params: { month } }); }
}
