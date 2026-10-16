import { Transaction } from './transaction';

export interface TransactionQuery {
  accountId?: number;
  categoryId?: number;
  categoryType?: string;
  startDate: string;
  endDate: string;
  search?: string;
  page: number;
  pageSize: number;
}

export interface TransactionPage {
  items: Transaction[];
  totalCount: number;
  page: number;
  pageSize: number;
}
