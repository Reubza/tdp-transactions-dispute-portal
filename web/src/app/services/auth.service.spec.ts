import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { LoginResponse } from '../models/auth.model';

const loginResponse = (expiresInMs = 60_000): LoginResponse => ({
  accessToken: 'token-123',
  expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
  user: { id: 'u1', fullName: 'Thandi Nkosi', email: 'thandi@example.com', role: 'Customer' },
});

describe('AuthService', () => {
  let http: HttpTestingController;

  function setup(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => http.verify());

  it('stores the session after a successful login', () => {
    const auth = setup();

    auth.login('thandi@example.com', 'secret').subscribe();
    const req = http.expectOne('/api/v1/auth/login');
    expect(req.request.body).toEqual({ email: 'thandi@example.com', password: 'secret' });
    req.flush(loginResponse());

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken).toBe('token-123');
    expect(auth.user()?.fullName).toBe('Thandi Nkosi');
    expect(sessionStorage.getItem('dispute-portal.session')).toContain('token-123');
  });

  it('restores a valid session from storage', () => {
    sessionStorage.setItem('dispute-portal.session', JSON.stringify(loginResponse()));

    const auth = setup();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.hasRole('Customer')).toBe(true);
    expect(auth.hasRole('Agent')).toBe(false);
  });

  it('ignores an expired stored session', () => {
    sessionStorage.setItem('dispute-portal.session', JSON.stringify(loginResponse(-1000)));

    const auth = setup();

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('clears the session and returns to login on logout', () => {
    sessionStorage.setItem('dispute-portal.session', JSON.stringify(loginResponse()));
    const auth = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    auth.logout({ returnUrl: '/disputes' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('dispute-portal.session')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/disputes' } });
  });

  it('routes agents and customers to different home pages', () => {
    const auth = setup();

    expect(auth.homeFor('Agent')).toBe('/agent/disputes');
    expect(auth.homeFor('Customer')).toBe('/transactions');
  });
});
