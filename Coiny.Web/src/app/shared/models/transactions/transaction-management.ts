import { Transaction } from './transaction';
export interface TransactionEditDetails {
  transaction: Transaction;
  pairedEditVersion: number | null;
  bill: { allocationKey: string; dueDate: string; isPaid: boolean } | null;
}
export interface UpdateTransactionRequest {
  expectedEditVersion: number; expectedPairedEditVersion: number | null;
  accountId: number; categoryType: 'Income' | 'Expense' | 'Transfer'; categoryId: number | null;
  amount: number; date: string; description: string; merchant: string | null; notes: string | null; isCleared: boolean;
  direction: 'In' | 'Out' | null; counterparty: 'Unknown' | 'External' | 'Linked' | null;
}
