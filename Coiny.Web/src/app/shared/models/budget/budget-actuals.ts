import { AllocationType } from './budget';
export interface BudgetActualsItem {
  allocationKey: string; name: string; allocationType: AllocationType;
  planned: number; actual: number; remaining: number; awaitingTransaction: number; transactionCount: number;
}
export interface BudgetCategoryDefault {
  categoryId: number; categoryName: string; isActive: boolean; allocationKey: string | null;
  effectiveFromMonth: string | null; editVersion: number;
}
export interface BudgetActualsTransaction {
  id: number; editVersion: number; pairedEditVersion: number | null; date: string;
  description: string; merchant?: string | null; amount: number; accountName: string; categoryName: string;
  categoryType: 'Expense' | 'Transfer'; allocationKey: string | null; allocationName: string | null;
  assignmentSource: 'Payment' | 'Override' | 'Category' | 'Unassigned';
}
export interface BudgetActuals {
  startDate?: string; endDate?: string;
  points?: BudgetComparisonPoint[];
  month: string; throughDate: string | null; planned: number; assignedActual: number; unassignedExpenses: number;
  awaitingTransaction: number; items: BudgetActualsItem[]; categoryDefaults: BudgetCategoryDefault[];
  transactions: BudgetActualsTransaction[]; page: number; totalCount: number; pageSize: number;
}
export interface BudgetComparisonPoint {
  date: string; plannedSpending: number; actualSpending: number | null;
  plannedContributions: number; actualContributions: number | null;
}
