import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

import { AppHeader } from '../../shared/components/app-header/app-header';
import { AppFooter } from '../../shared/components/app-footer/app-footer';

@Component({
  selector: 'app-app-layout',
  imports: [RouterOutlet, AppHeader, AppFooter],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.css',
})
export class AppLayout {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
