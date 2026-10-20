export interface TransactionImportRow {
  rowId: number; date: string | null; amount: number | null; direction: 'In' | 'Out' | null;
  categoryType: 'Income' | 'Expense' | 'Transfer'; categoryId: number | null;
  counterparty: 'Unknown' | 'External'; description: string; merchant: string | null;
  notes: string | null; isCleared: boolean; allowDuplicate: boolean;
}
export interface BankBalanceReference { date: string; balance: number; }
export interface BankBalanceComparison { date: string; bankBalance: number; calculatedBalance: number; difference: number; requiresAcknowledgment: boolean; }
export interface TransactionImportRequest {
  bankBalanceCheck?: BankBalanceReference & { acknowledgedCalculatedBalance?: number | null };
  accountId: number; submissionId: string; filename: string; rows: TransactionImportRow[];
}
export interface ImportRowCheck { rowId: number; errors: string[]; duplicateTransactionIds: number[]; duplicateRowIds: number[]; }
export interface TransactionImportValidation { rows: ImportRowCheck[]; canImport: boolean; currentBalance: number; projectedBalance: number | null; balanceComparison?: BankBalanceComparison | null; }
export interface TransactionImportResult {
  batchId: number; submissionId: string; accountId: number; filename: string;
  importedCount: number; completedAt: string; balanceAfter: number;
}
