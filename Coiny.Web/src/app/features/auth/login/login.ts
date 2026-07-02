import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

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

  model: LoginRequest = {
    email: '',
    password: ''
  };

  login(): void {

    this.authService.login(this.model).subscribe({

      next: response => {
        console.log(response);
      },

      error: error => {
        console.error(error);
      }

    });

  }

}