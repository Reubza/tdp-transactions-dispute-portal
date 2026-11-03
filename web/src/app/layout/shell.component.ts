import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  template: `
    <a class="skip-link" href="#main">Skip to content</a>
    <mat-toolbar class="toolbar">
      <a class="brand" [routerLink]="home()">
        <mat-icon aria-hidden="true">account_balance</mat-icon>
        <span>Dispute Portal</span>
      </a>
      <nav class="nav" aria-label="Main">
        @for (item of nav(); track item.path) {
          <a
            mat-button
            [routerLink]="item.path"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
          >
            <mat-icon>{{ item.icon }}</mat-icon>
            <span class="nav-label">{{ item.label }}</span>
          </a>
        }
      </nav>
      <span class="spacer"></span>
      @if (auth.user(); as user) {
        <button mat-button [matMenuTriggerFor]="menu" aria-label="Account menu">
          <mat-icon>account_circle</mat-icon>
          <span class="nav-label">{{ user.fullName }}</span>
        </button>
        <mat-menu #menu="matMenu">
          <div class="menu-header">
            <strong>{{ user.fullName }}</strong>
            <small>{{ user.email }} · {{ user.role }}</small>
          </div>
          <button mat-menu-item (click)="auth.logout()">
            <mat-icon>logout</mat-icon>
            <span>Sign out</span>
          </button>
        </mat-menu>
      }
    </mat-toolbar>
    <main id="main" class="content" tabindex="-1">
      <router-outlet />
    </main>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      gap: 8px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      --mat-button-text-label-text-color: var(--mat-sys-on-primary);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: inherit;
      text-decoration: none;
      font-weight: 500;
      margin-right: 16px;
    }
    .nav {
      display: flex;
      gap: 4px;
    }
    .nav a.active {
      background: color-mix(in srgb, var(--mat-sys-on-primary) 16%, transparent);
    }
    .spacer {
      flex: 1;
    }
    .content {
      flex: 1;
      width: 100%;
      max-width: 1200px;
      margin: 0 auto;
      padding: 24px 16px 48px;
      box-sizing: border-box;
      outline: none;
    }
    .menu-header {
      display: flex;
      flex-direction: column;
      padding: 8px 16px 12px;
    }
    .skip-link {
      position: absolute;
      left: -9999px;
    }
    .skip-link:focus {
      left: 8px;
      top: 8px;
      z-index: 100;
      background: var(--mat-sys-surface);
      padding: 8px;
    }
    @media (max-width: 720px) {
      .nav-label,
      .brand span {
        display: none;
      }
      .content {
        padding: 16px 8px 32px;
      }
    }
  `,
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);

  protected readonly home = computed(() => this.auth.homeFor(this.auth.user()?.role));

  protected readonly nav = computed<NavItem[]>(() =>
    this.auth.user()?.role === 'Agent'
      ? [{ path: '/agent/disputes', label: 'Dispute queue', icon: 'inbox' }]
      : [
          { path: '/transactions', label: 'Transactions', icon: 'receipt_long' },
          { path: '/disputes', label: 'My disputes', icon: 'history' },
        ],
  );
}
