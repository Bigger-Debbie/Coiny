import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AccountService } from '../../../core/services/account.service';
import { CategoryService } from '../../../core/services/category.service';
import { TransactionService } from '../../../core/services/transaction.service';

import { Account } from '../../../shared/models/account/account';
import { Category } from '../../../shared/models/category/category';
import { CreateTransactionRequest } from '../../../shared/models/transactions/create-transaction-request';

@Component({
  selector: 'app-create-transaction',
  imports: [FormsModule],
  templateUrl: './create-transaction.html',
  styleUrl: './create-transaction.css',
})
export class CreateTransaction implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly categoryService = inject(CategoryService);
  private readonly transactionService = inject(TransactionService);
  private readonly router = inject(Router);

  accounts: Account[] = [];
  categories: Category[] =[];

  request: CreateTransactionRequest = {
    accountId: 0,
    categoryId: 0,
    amount: 0,
    transactionDate: new Date().toISOString().split('T')[0],
    description: '',
    merchant: null,
    notes: null,
    isCleared: false
  };

  ngOnInit(): void {
    this.loadAccounts();
    this.loadCategories();
  }

  private loadAccounts(): void {
    this.accountService.getAccounts().subscribe({
      next: accounts => {
        this.accounts = accounts;
      },
      error: error => {
        console.error(error);
      }
    });
  }

  private loadCategories(): void { 
    this.categoryService.getCategories().subscribe({
      next: categories => {
        this.categories = categories;
      },
      error: error => {
        console.error(error);
      }
    });
  }

  save(): void {
    this.transactionService.CreateTransaction(this.request).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: error => {
        console.error(error);
      }
    });
  }
}
