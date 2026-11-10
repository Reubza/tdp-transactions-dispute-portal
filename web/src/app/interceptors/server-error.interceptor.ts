import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

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
