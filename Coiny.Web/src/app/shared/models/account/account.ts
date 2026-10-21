import { AccountReconciliation } from './account-reconciliation';

export interface Account {
    latestReconciliation?: AccountReconciliation | null;
    id: number;
    institutionId: number | null;
    institutionName: string | null;

    name: string; 

    loanSubtype?: string | null;
    accountType: string;

    openingBalance: number;
    openingBalanceDate: string;
    currentBalance: number;

    isActive: boolean;
}
