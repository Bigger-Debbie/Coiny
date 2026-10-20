import { TransferDetails } from './transfer';

export interface Transaction extends TransferDetails {
    id: number;
    editVersion?: number;
    notes?: string | null;
    importBatchId?: number | null;
    accountId: number;
    categoryId: number;
    accountName: string;
    institutionName: string | null;
    categoryName: string;
    categoryType: string;
    amount: number;
    transactionDate: string;
    description: string;
    merchant: string | null;
    isCleared: boolean;
}
