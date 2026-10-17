import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { CreateTransfer, CreateTransferEntry } from '../../shared/models/transactions/transfer';
import { Transaction } from '../../shared/models/transactions/transaction';

@Injectable({ providedIn: 'root' })
export class TransferService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/transfers`;
  create(request: CreateTransfer) {
    return this.http.post<{ outgoing: Transaction; incoming: Transaction }>(this.url, request);
  }
  createEntry(request: CreateTransferEntry) {
    return this.http.post<Transaction>(`${this.url}/entries`, request);
  }
}
