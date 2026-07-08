import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../shared/models/auth/login-request';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  model: LoginRequest = {
    email: '',
    password: ''
  };

  login(): void {

    this.authService.login(this.model).subscribe({

      next: response => {
        this.authService.setToken(response.token);
        this.router.navigate(['/dashboard']);
      },

      error: error => {
        console.error(error);
      }

    });

  }

}