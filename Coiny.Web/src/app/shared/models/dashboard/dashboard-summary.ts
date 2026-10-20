import { DashboardAccount } from "./dashboard-account";
import { DashboardTransaction } from "./dashboard-transaction";

export interface DashboardSummary {
    netWorth: {
        currentValue: number;
        retirement: number;
        liquidFunds: number;
        debt: number;
        investments?: number;
        hasInvestments?: boolean;
        mortgage?: number;
        hasMortgage?: boolean;
        includeMortgageInNetWorth?: boolean;
    };
    accounts: DashboardAccount[];
    recentTransactions: DashboardTransaction[];
}
