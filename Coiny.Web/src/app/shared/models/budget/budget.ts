export type BudgetStep = 'Income' | 'Bills' | 'Spending' | 'SavingsAndInvestments' | 'Review';
export type AllocationType = 'Housing' | 'Bills' | 'Spending' | 'Savings' | 'Investments';
export type BudgetFrequency = 'Weekly' | 'EveryTwoWeeks' | 'TwiceMonthly' | 'Monthly' | 'EverySixMonths' | 'Yearly';
export interface BudgetSchedule {
  frequency: BudgetFrequency; startDate: string; endDate: string | null;
  dayOfMonth: number | null; secondDayOfMonth: number | null;
}
export interface BudgetIncome { sourceKey?: string | null; name: string; amount: number; healthInsurancePerPaycheck?: number | null; retirementContributionPerPaycheck?: number | null; schedule: BudgetSchedule; }
export interface BudgetAllocation { sourceKey?: string | null; name: string; amount: number; allocationType: AllocationType; schedule: BudgetSchedule | null; }
export interface BudgetDraftRequest {
  expectedEditVersion: number; effectiveFromMonth: string; setupStep: BudgetStep;
  incomeSources: BudgetIncome[]; allocations: BudgetAllocation[];
}
export interface BudgetRevision extends Omit<BudgetDraftRequest, 'expectedEditVersion'> {
  id: number; revisionNumber: number; editVersion: number; state: 'Draft' | 'Published'; publishedAt: string | null;
}
export interface BudgetSetup {
  currentMonth: string; hasPublishedBudget: boolean; firstPublishedMonth: string | null;
  currentRevisionId: number | null; draft: BudgetRevision | null;
}
export interface BudgetMonthlyItem { id: number; name: string; allocationType: AllocationType | null; plannedAmount: number; healthInsurance?: number; retirementContributions?: number; occurrenceCount: number; }
export interface BudgetEvent { payment?: BillPaymentState | null; sourceKey?: string | null; date: string; itemId: number; name: string; isIncome: boolean; allocationType: AllocationType | null; amount: number; }
export interface BudgetMonth {
  month: string; revisionId: number | null; healthInsurance: number; retirementContributions: number; expectedIncome: number; allocated: number;
  unassigned: number; overBudget: number; incomeSources: BudgetMonthlyItem[];
  allocations: BudgetMonthlyItem[]; events: BudgetEvent[];
}
export interface BudgetPreview { draftId: number; editVersion: number; month: BudgetMonth; }

export interface BudgetIncomePeriod {
  asOf: string; sourceKey: string | null; sources: { sourceKey: string; name: string }[];
  startDate: string | null; endDate: string | null; nextPayday: string | null;
  unpaidBillsTotal?: number; billsTotal: number; contributionsTotal: number; bills: BudgetEvent[]; contributions: BudgetEvent[];
}

export interface BillPaymentState { linkedTransaction?: { id: number; date: string; amount: number; description: string } | null; isPaid: boolean; paidOn: string | null; note: string | null; transactionId: number | null; }
export interface BillTransactionCandidate { id: number; date: string; amount: number; description: string; accountName: string; categoryName: string; exactAmount: boolean; }
