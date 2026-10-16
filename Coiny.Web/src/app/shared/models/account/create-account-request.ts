export interface CreateAccountRequest {
    name: string;
    accountType: string;
    institutionId: number | null;
    openingBalance: number;
    openingBalanceDate: string;
}
