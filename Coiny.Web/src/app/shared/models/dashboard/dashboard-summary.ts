import { DashboardAccount } from "./dashboard-account";
import { DashboardTransaction } from "./dashboard-transaction";

export interface DashboardSummary {
    netWorth: {
        currentValue: number;
        retirement: number;
        liquidFunds: number;
        debt: number;
    };
    accounts: DashboardAccount[];
    recentTransactions: DashboardTransaction[];
}
