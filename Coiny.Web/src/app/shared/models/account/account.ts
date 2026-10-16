export interface Account {
    id: number;
    institutionId: number | null;
    institutionName: string | null;

    name: string; 

    accountType: string;

    openingBalance: number;
    openingBalanceDate: string;
    currentBalance: number;

    isActive: boolean;
}
