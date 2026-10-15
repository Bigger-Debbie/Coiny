export interface DashboardAccount {
    id: number;
    name: string;
    accountType: string;
    currentBalance: number;
    institutionName: string | null;
    monthlyTrend: {
        startDate: string;
        endDate: string;
        absoluteChange: number | null;
        percentageChange: number | null;
        direction: 'Up' | 'Down' | 'Neutral';
    };
}
