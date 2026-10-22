export type BreakdownType = 'Expense' | 'Income';
export interface CategoryBreakdownItem {
  color?: string;
  strokeWidth?: number;
  categoryId: number;
  categoryName: string;
  amount: number;
  transactionCount: number;
  percentage: number;
}
export interface CategoryBreakdown {
  categoryType: BreakdownType;
  totalAmount: number;
  transactionCount: number;
  categories: CategoryBreakdownItem[];
}
export interface CategoryBreakdownQuery {
  accountId?: number;
  startDate: string;
  endDate: string;
  categoryType: BreakdownType;
}
