import { Component, Input } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Icon } from '../../../shared/components/icon/icon';
import { DashboardAccount } from '../../../shared/models/dashboard/dashboard-account';

@Component({
  selector: 'app-account-list',
  imports: [CurrencyPipe, DecimalPipe, Icon],
  templateUrl: './account-list.html',
  styleUrl: './account-list.css',
})
export class AccountList {
  @Input() accounts: readonly DashboardAccount[] = [];
  readonly magnitude = Math.abs;

  initial(account: DashboardAccount): string {
    return Array.from(account.institutionName?.trim() || account.name.trim())[0]?.toUpperCase() || '?';
  }

  isLiability(account: DashboardAccount): boolean {
    return account.accountType === 'CreditCard' || account.accountType === 'Loan';
  }

  isImproving(account: DashboardAccount): boolean {
    return this.isLiability(account)
      ? account.monthlyTrend.direction === 'Down'
      : account.monthlyTrend.direction === 'Up';
  }

  trendDescription(account: DashboardAccount): string {
    const trend = account.monthlyTrend;
    if (trend.absoluteChange === null) return 'No balance history for the past month';
    const movement = trend.direction === 'Up' ? 'increased' : trend.direction === 'Down' ? 'decreased' : 'was unchanged';
    const change = trend.percentageChange === null
      ? new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(Math.abs(trend.absoluteChange))
      : `${Math.abs(trend.percentageChange).toFixed(1)}%`;
    const baseline = trend.percentageChange === null ? ' Percentage unavailable because the starting balance was zero.' : '';
    return `Balance ${movement}${trend.direction === 'Neutral' ? '' : ` by ${change}`} from ${trend.startDate} to ${trend.endDate}.${baseline}`;
  }
}
