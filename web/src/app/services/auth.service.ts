import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, UserProfile, UserRole } from '../models/auth.model';

const STORAGE_KEY = 'dispute-portal.session';

interface Session {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

/**
 * Holds the signed-in session. The token lives in sessionStorage so it survives a page refresh but not the
 * browser tab, and is discarded as soon as it expires.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(this.restore());
  private expiryTimer?: ReturnType<typeof setTimeout>;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  constructor() {
    this.scheduleExpiry();
  }

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  hasRole(role: UserRole): boolean {
    return this.user()?.role === role;
  }

  /** Where a user of the given role lands after signing in. */
  homeFor(role: UserRole | undefined): string {
    return role === 'Agent' ? '/agent/disputes' : '/transactions';
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/v1/auth/login', { email, password }).pipe(
      tap((response) => {
        const session: Session = {
          accessToken: response.accessToken,
          expiresAt: response.expiresAt,
          user: response.user,
        };
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        this.session.set(session);
        this.scheduleExpiry();
      }),
    );
  }

  logout(options: { returnUrl?: string; expired?: boolean } = {}): void {
    sessionStorage.removeItem(STORAGE_KEY);
    clearTimeout(this.expiryTimer);
    this.session.set(null);
    const queryParams: Record<string, string> = {};
    if (options.returnUrl) queryParams['returnUrl'] = options.returnUrl;
    if (options.expired) queryParams['expired'] = 'true';
    void this.router.navigate(['/login'], { queryParams });
  }

  private restore(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }

  private scheduleExpiry(): void {
    clearTimeout(this.expiryTimer);
    const session = this.session();
    if (!session) return;
    const remaining = new Date(session.expiresAt).getTime() - Date.now();
    this.expiryTimer = setTimeout(() => this.logout({ expired: true }), Math.max(remaining, 0));
  }
}
