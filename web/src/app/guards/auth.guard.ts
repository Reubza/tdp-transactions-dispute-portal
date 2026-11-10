import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/auth.model';

/** Allows the route only for a signed-in user with the role in `route.data.role`. */
export const roleGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  const required = route.data['role'] as UserRole | undefined;
  if (required && !auth.hasRole(required)) {
    return router.createUrlTree([auth.homeFor(auth.user()?.role)]);
  }

  return true;
};

/** Sends already signed-in users away from the login page. */
export const anonymousGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? inject(Router).createUrlTree([auth.homeFor(auth.user()?.role)])
    : true;
};

/** Redirects the bare root URL to the signed-in user's home page (or the login page). */
export const homeRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated()
    ? router.createUrlTree([auth.homeFor(auth.user()?.role)])
    : router.createUrlTree(['/login']);
};
