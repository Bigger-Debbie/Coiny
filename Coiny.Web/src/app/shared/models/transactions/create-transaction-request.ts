export interface CreateTransactionRequest {
    accountId: number;
    categoryId: number;
    amount: number;
    transactionDate: string;
    description: string;
    merchant: string | null;
    notes: string | null;
    isCleared: boolean;
}