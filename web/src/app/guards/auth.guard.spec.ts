import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { roleGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/auth.model';

describe('roleGuard', () => {
  function run(
    user: { role: UserRole } | null,
    requiredRole: UserRole,
    url = '/transactions',
  ): boolean | UrlTree {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => user !== null,
            user: () => user,
            hasRole: (role: UserRole) => user?.role === role,
            homeFor: (role?: UserRole) => (role === 'Agent' ? '/agent/disputes' : '/transactions'),
          },
        },
      ],
    });
    const route = { data: { role: requiredRole } } as unknown as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => roleGuard(route, state)) as boolean | UrlTree;
  }

  const serialize = (result: boolean | UrlTree) =>
    TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('sends anonymous users to login with a return URL', () => {
    const result = run(null, 'Customer', '/disputes');

    expect(serialize(result)).toBe('/login?returnUrl=%2Fdisputes');
  });

  it('allows users with the required role', () => {
    expect(run({ role: 'Customer' }, 'Customer')).toBe(true);
  });

  it('redirects users with the wrong role to their own home page', () => {
    const result = run({ role: 'Agent' }, 'Customer');

    expect(serialize(result)).toBe('/agent/disputes');
  });
});
