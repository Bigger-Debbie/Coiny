export interface DashboardTransaction {
    id: number;
    accountName: string;
    institutionName: string | null;
    categoryName: string;
    categoryType: string;
    amount: number;
    transactionDate: string;
    description: string;
    merchant: string | null;
}