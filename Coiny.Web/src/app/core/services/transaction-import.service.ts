import { ImportUndoPreview, ImportUndoRequest, ImportUndoResult, ImportUndoSelection } from '../../shared/models/transactions/import-undo';
import { ImportSessionRequest, ImportSessionResult, ImportSessionValidation } from '../../shared/models/transactions/import-session';
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environemnt } from '../../../environments/environment';
import { TransactionImportRequest, TransactionImportResult, TransactionImportValidation } from '../../shared/models/transactions/transaction-import';

@Injectable({ providedIn: 'root' })
export class TransactionImportService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environemnt.apiUrl}/transaction-imports`;
  validateSession(request: ImportSessionRequest) { return this.http.post<ImportSessionValidation>(`${this.url}/sessions/validate`,request); }
  importSession(request: ImportSessionRequest) { return this.http.post<ImportSessionResult>(`${this.url}/sessions`,request); }
  validate(request: TransactionImportRequest) { return this.http.post<TransactionImportValidation>(`${this.url}/validate`, request); }
  import(request: TransactionImportRequest) { return this.http.post<TransactionImportResult>(this.url, request); }
  review(batchId:number) { return this.http.get<ImportUndoPreview>(`${this.url}/${batchId}/review`); }
  previewUndo(request:ImportUndoSelection) { return this.http.post<ImportUndoPreview>(`${this.url}/undo/preview`,request); }
  undo(request:ImportUndoRequest) { return this.http.post<ImportUndoResult>(`${this.url}/undo`,request); }
  history(accountId?: number) { return this.http.get<TransactionImportResult[]>(this.url, { params: accountId ? { accountId } : {} }); }
}
