import { Component, Input } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Icon } from '../../../shared/components/icon/icon';
import { DashboardTransaction } from '../../../shared/models/dashboard/dashboard-transaction';

@Component({
  selector: 'app-transaction-list',
  imports: [CurrencyPipe, DatePipe, Icon],
  templateUrl: './transaction-list.html',
  styleUrl: './transaction-list.css',
})
export class TransactionList {
  @Input() transactions: readonly DashboardTransaction[] = [];
  readonly magnitude = Math.abs;

  initial(transaction: DashboardTransaction): string {
    return Array.from(transaction.institutionName?.trim() || transaction.accountName.trim())[0]?.toUpperCase() || '?';
  }

  calendarDate(transaction: DashboardTransaction): string {
    // A ledger date is a calendar day; don't shift it into the browser timezone.
    return transaction.transactionDate.slice(0, 10);
  }
}
