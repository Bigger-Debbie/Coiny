import { BudgetSchedule } from './budget';
export interface RecurringSuggestion {
  reviewed?: boolean; key: string; merchant: string; frequency: BudgetSchedule['frequency']; typicalAmount: number; minimumAmount: number; maximumAmount: number;
  lastSeen: string; nextExpected: string; transactionCount: number; status: 'NeedsReview' | 'Covered' | 'Dismissed'; allocationKey: string | null; suggestedAllocationKey: string | null;
  transactions: { id: number; date: string; amount: number; accountName: string; description: string }[];
}
export interface RecurringScan {
  fromDate: string; throughDate: string; transactionsScanned: number; suggestions: RecurringSuggestion[]; budgetItems: { key: string; name: string }[];
}
