import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Attaches the bearer token to API calls and signs the user out when the API rejects it. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken;
  const isApi = req.url.startsWith('/api/');
  const isLogin = req.url.endsWith('/auth/login');

  const request =
    token && isApi && !isLogin
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin) {
        auth.logout({ returnUrl: router.url, expired: true });
      }
      return throwError(() => error);
    }),
  );
};

/** Surfaces transport failures and server errors that individual screens don't handle themselves. */
export const serverErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
        const message =
          error.status === 0
            ? 'Unable to reach the server. Check your connection and try again.'
            : 'Something went wrong on our side. Please try again.';
        snackBar.open(message, 'Dismiss', { duration: 6000 });
      }
      return throwError(() => error);
    }),
  );
};
