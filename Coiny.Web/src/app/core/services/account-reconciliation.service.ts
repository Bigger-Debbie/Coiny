import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { AccountReconciliation, ReconciliationCheck } from '../../shared/models/account/account-reconciliation';

@Injectable({ providedIn: 'root' })
export class AccountReconciliationService {
  private readonly http = inject(HttpClient);
  private url(id: number) { return `${environemnt.apiUrl}/accounts/${id}/reconciliations`; }
  history(id: number) { return this.http.get<AccountReconciliation[]>(this.url(id)); }
  check(id: number, statementDate: string, statementBalance: number) {
    return this.http.post<ReconciliationCheck>(`${this.url(id)}/check`, { statementDate, statementBalance });
  }
  confirm(id: number, check: ReconciliationCheck) {
    return this.http.post<AccountReconciliation>(this.url(id), {
      statementDate: check.statementDate, statementBalance: check.statementBalance,
      reviewedFingerprint: check.ledgerFingerprint, transactionsReviewed: true,
    });
  }
}
