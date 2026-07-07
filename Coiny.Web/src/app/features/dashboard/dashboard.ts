import { Component } from '@angular/core';
import { inject, OnInit } from '@angular/core';

import { AccountService } from '../../core/services/account.service';
import { Account } from '../../shared/models/account/account';

@Component({
  selector: 'app-dashboard',
  imports: [],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly accountService = inject(AccountService);

  accounts: Account[] = [];

  ngOnInit(): void {
    this.accountService.getAccounts().subscribe({
      next: accounts => {
        this.accounts = accounts;

        console.log(accounts);
      },

      error: error => {
        console.log(error);
      }
    })
  }
}
