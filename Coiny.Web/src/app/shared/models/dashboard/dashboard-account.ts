export interface DashboardAccount {
    id: number;
    name: string;
    accountType: string;
    currentBalance: number;
    institutionName: string | null;
}