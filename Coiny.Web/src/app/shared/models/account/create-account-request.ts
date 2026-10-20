export interface CreateAccountRequest {
    name: string;
    loanSubtype?: string | null;
    accountType: string;
    institutionId: number | null;
    openingBalance: number;
    openingBalanceDate: string;
}
