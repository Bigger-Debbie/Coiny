import { Component, Input, output } from '@angular/core';
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
  @Input() interactive = false;
  readonly open = output<number>();
  readonly magnitude = Math.abs;

  sign(transaction: DashboardTransaction): string {
    if (transaction.categoryType === 'Transfer') return transaction.transferDirection === 'In' ? '+' : transaction.transferDirection === 'Out' ? '−' : '';
    return transaction.categoryType === 'Income' ? '+' : transaction.categoryType === 'Expense' ? '−' : '';
  }
  transferLabel(transaction: DashboardTransaction): string {
    const direction = transaction.transferDirection === 'In' ? 'in' : transaction.transferDirection === 'Out' ? 'out' : '';
    if (transaction.counterpartyAccountName) {
      if (transaction.transferDirection === 'In') {
        if (['Investment', 'Retirement'].includes(transaction.accountType ?? '') &&
          ['Checking', 'Savings', 'Cash'].includes(transaction.counterpartyAccountType ?? ''))
          return `Contribution from ${transaction.counterpartyAccountName}`;
        if (['CreditCard', 'Loan'].includes(transaction.accountType ?? ''))
          return `Payment from ${transaction.counterpartyAccountName}`;
      }
      return `Transfer ${transaction.transferDirection === 'In' ? 'from' : 'to'} ${transaction.counterpartyAccountName}`;
    }
    if (transaction.transferCounterparty === 'External') return `Transfer ${direction} · External account`;
    if (transaction.transferCounterparty === 'Unknown') return `Transfer ${direction} · Not linked`;
    return 'Transfer';
  }

  initial(transaction: DashboardTransaction): string {
    return Array.from(transaction.institutionName?.trim() || transaction.accountName.trim())[0]?.toUpperCase() || '?';
  }

  calendarDate(transaction: DashboardTransaction): string {
    // A ledger date is a calendar day; don't shift it into the browser timezone.
    return transaction.transactionDate.slice(0, 10);
  }
}
