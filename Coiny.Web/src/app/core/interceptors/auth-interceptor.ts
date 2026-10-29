import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { environemnt } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService); const router = inject(Router);
  const token = auth.getToken();
  const apiRequest = req.url.startsWith(environemnt.apiUrl + '/');
  // Never send the session token to third-party URLs.
  if (!token || !apiRequest) return next(req);
  const publicAuth = /\/auth\/(login|register|forgot-password|reset-password|recovery-options|recover-with-code)$/.test(req.url.split('?')[0]);
  if (publicAuth) return next(req);
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(catchError(error => {
    if (error.status === 401 && auth.getToken() === token) {
      auth.logout(); void router.navigate(['/login'], { replaceUrl: true });
    }
    return throwError(() => error);
  }));
};
