export interface TransferDetails {
  accountType?: string;
  transferDirection?: 'In' | 'Out' | null;
  transferCounterparty?: 'Unknown' | 'External' | 'Linked' | null;
  pairedTransactionId?: number | null;
  counterpartyAccountId?: number | null;
  counterpartyAccountName?: string | null;
  counterpartyAccountType?: string | null;
}

export interface CreateTransfer {
  fromAccountId: number;
  toAccountId: number;
  amount: number;
  date: string;
  description: string | null;
  notes: string | null;
  isCleared: boolean;
}

export interface CreateTransferEntry {
  accountId: number;
  amount: number;
  date: string;
  direction: 'In' | 'Out';
  counterparty: 'Unknown' | 'External';
  description: string;
  notes: string | null;
  isCleared: boolean;
}
