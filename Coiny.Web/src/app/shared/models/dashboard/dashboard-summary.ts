import { DashboardAccount } from "./dashboard-account";
import { DashboardTransaction } from "./dashboard-transaction";

export interface DashboardSummary {
    netWorth: number;
    accounts: DashboardAccount[];
    recentTransactions: DashboardTransaction[];
}