import { TransferDetails } from '../transactions/transfer';

export interface DashboardTransaction extends TransferDetails {
    id: number;
    accountName: string;
    institutionName: string | null;
    categoryName: string;
    categoryType: string;
    amount: number;
    runningBalance?: number | null;
    transactionDate: string;
    description: string;
    merchant: string | null;
}
