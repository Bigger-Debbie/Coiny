export interface AccountReconciliation {
  id: number;
  statementDate: string;
  statementBalance: number;
  confirmedAt: string;
  confirmedByName: string;
  needsReview: boolean;
  invalidatedAt: string | null;
  invalidationReason: string | null;
}

export interface ReconciliationCheck {
  statementDate: string;
  statementBalance: number;
  calculatedBalance: number;
  difference: number;
  matches: boolean;
  ledgerFingerprint: string;
  transactionCount: number;
}
