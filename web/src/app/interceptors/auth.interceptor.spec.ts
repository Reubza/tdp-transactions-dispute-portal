import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  const auth = { accessToken: 'abc' as string | null, logout: vi.fn() };

  beforeEach(() => {
    auth.accessToken = 'abc';
    auth.logout.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds the bearer token to API requests', () => {
    http.get('/api/v1/transactions').subscribe();

    const req = controller.expectOne('/api/v1/transactions');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc');
    req.flush({});
  });

  it('does not send the token to non-API URLs or the login endpoint', () => {
    http.get('/assets/config.json').subscribe();
    http.post('/api/v1/auth/login', {}).subscribe();

    expect(controller.expectOne('/assets/config.json').request.headers.has('Authorization')).toBe(
      false,
    );
    expect(controller.expectOne('/api/v1/auth/login').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('signs the user out when the API returns 401', () => {
    http.get('/api/v1/disputes').subscribe({ error: () => undefined });

    controller
      .expectOne('/api/v1/disputes')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).toHaveBeenCalledWith(expect.objectContaining({ expired: true }));
  });

  it('does not sign out on a failed login attempt', () => {
    http.post('/api/v1/auth/login', {}).subscribe({ error: () => undefined });

    controller
      .expectOne('/api/v1/auth/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).not.toHaveBeenCalled();
  });
});
