import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { TransactionImportRequest, TransactionImportResult, TransactionImportValidation } from '../../shared/models/transactions/transaction-import';

@Injectable({ providedIn: 'root' })
export class TransactionImportService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/transaction-imports`;
  validate(request: TransactionImportRequest) { return this.http.post<TransactionImportValidation>(`${this.url}/validate`, request); }
  import(request: TransactionImportRequest) { return this.http.post<TransactionImportResult>(this.url, request); }
  history(accountId: number) { return this.http.get<TransactionImportResult[]>(this.url, { params: { accountId } }); }
}
