export interface Account {
    id: number;
    institutionId: number;
    institutionName: string;

    name: string; 

    accountType: string;

    openingBalance: number;
    currentBalance: number;

    isActive: boolean;
}