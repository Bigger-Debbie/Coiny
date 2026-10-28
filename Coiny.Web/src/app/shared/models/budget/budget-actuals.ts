import { AllocationType } from './budget';
export interface BudgetActualsItem {
  allocationKey: string; name: string; allocationType: AllocationType;
  priorPeriodActual?: number; futurePeriodActual?: number;
  planned: number; actual: number; remaining: number; awaitingTransaction: number; transactionCount: number;
}
export interface BudgetCategoryDefault {
  categoryId: number; categoryName: string; isActive: boolean; allocationKey: string | null;
  effectiveFromMonth: string | null; editVersion: number;
}
export interface BudgetActualsTransaction { fundedFromSavings?: number;
  id: number; editVersion: number; pairedEditVersion: number | null; date: string;
  description: string; merchant?: string | null; amount: number; accountName: string; categoryName: string;
  categoryType: 'Expense' | 'Transfer'; allocationKey: string | null; allocationName: string | null;
  paymentDueDate?: string | null;
  assignmentSource: 'Payment' | 'Override' | 'Category' | 'Unassigned' | 'Fund';
}
export interface BudgetActuals {
  startDate?: string; endDate?: string;
  points?: BudgetComparisonPoint[];
  month: string; throughDate: string | null; planned: number; assignedActual: number; unassignedExpenses: number;
  awaitingTransaction: number; items: BudgetActualsItem[]; categoryDefaults: BudgetCategoryDefault[];
  transactions: BudgetActualsTransaction[]; page: number; totalCount: number; pageSize: number;
}
export interface BudgetComparisonPoint {
  forecastSpending?: number | null; forecastContributions?: number | null;
  date: string; plannedSpending: number; actualSpending: number | null;
  plannedContributions: number; actualContributions: number | null;
}

export interface BudgetRemainderPoint { date: string; planned: number; actual: number | null; forecast: number | null; }
export interface BudgetForecastPeriod { startDate: string; endDate: string; plannedIncome: number; plannedAllocations: number; plannedRemainder: number; forecastIncome: number; forecastAllocations: number; forecastRemainder: number; cumulativePlanned: number; cumulativeForecast: number; }
export interface BudgetForecast {
  plannedSavingsContributions?: number; forecastSavingsContributions?: number;
  plannedInvestmentContributions?: number; forecastInvestmentContributions?: number;
  additionalSavings?: number; fundingShortfall?: number; potentialSavings?: number; shortfallBeyondSavings?: number;
  startDate: string; endDate: string; throughDate: string | null; months: number; expectedIncomeAwaitingTransaction: number; points: BudgetRemainderPoint[]; periods: BudgetForecastPeriod[]; }

export interface AvailableCashAccount { id:number; name:string; accountType:string; balance:number; isActive:boolean; }
export interface AvailableCashOutlook { balanceDate:string; endDate:string; cashBalance:number; cardBalance:number; expectedIncome:number; remainingAllocations:number; expectedRemaining:number|null; accountIds:number[]; accounts:AvailableCashAccount[]; }
