import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { CreateTransfer, CreateTransferEntry } from '../../shared/models/transactions/transfer';
import { Transaction } from '../../shared/models/transactions/transaction';

@Injectable({ providedIn: 'root' })
export class TransferService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/transfers`;
  creationCandidates(request: CreateTransfer) {
    return this.http.post<Transaction[]>(`${this.url}/candidates`, request);
  }
  candidates(id: number, accountId: number, days = 7) {
    return this.http.get<Transaction[]>(`${this.url}/entries/${id}/candidates`, { params: { accountId, days } });
  }
  link(firstTransactionId: number, secondTransactionId: number, expectedFirstEditVersion: number, expectedSecondEditVersion: number) {
    return this.http.post<{ outgoing: Transaction; incoming: Transaction }>(`${this.url}/links`, { firstTransactionId, secondTransactionId, expectedFirstEditVersion, expectedSecondEditVersion });
  }
  unlink(id: number, expectedEditVersion: number, expectedPairedEditVersion: number) {
    return this.http.delete<void>(`${this.url}/links/${id}`, { params: { expectedEditVersion, expectedPairedEditVersion } });
  }
  createCounterpart(id: number, accountId: number, date: string, expectedEditVersion: number) {
    return this.http.post<{ outgoing: Transaction; incoming: Transaction }>(`${this.url}/entries/${id}/counterpart`, { accountId, date, expectedEditVersion });
  }
  create(request: CreateTransfer) {
    return this.http.post<{ outgoing: Transaction; incoming: Transaction }>(this.url, request);
  }
  createEntry(request: CreateTransferEntry) {
    return this.http.post<Transaction>(`${this.url}/entries`, request);
  }
}
