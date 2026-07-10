import { Component } from '@angular/core';
import { inject, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router } from '@angular/router';

import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardSummary } from '../../shared/models/dashboard/dashboard-summary';

@Component({
  selector: 'app-dashboard',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly router = inject(Router);

  dashboard: DashboardSummary | null = null;

  ngOnInit(): void {
    this.dashboardService.getSummary().subscribe({
      next: summary => {
        this.dashboard = summary;
        console.log(summary);
      },
      error: error => {
        console.log(error);
      }
    });
  }

  goToCreateTransaction(): void {
    this.router.navigate(['/transactions/new']);
  }
}
