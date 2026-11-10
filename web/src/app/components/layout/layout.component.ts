import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-layout',
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
      <div class="toolbar-inner">
        <a class="brand" [routerLink]="home()" aria-label="Resolve home">
          <img src="logo.svg" alt="" width="32" height="32" />
          <span class="brand-text">
            <strong>Resolve</strong>
            <small>transaction disputes</small>
          </span>
        </a>
        <nav class="nav" aria-label="Main">
          @for (item of nav(); track item.path) {
            <a
              class="nav-link"
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
          <button class="user" type="button" [matMenuTriggerFor]="menu" aria-label="Account menu">
            <span class="avatar" aria-hidden="true">{{ initials() }}</span>
            <span class="user-text">
              <strong>{{ user.fullName }}</strong>
              <small>{{ user.role === 'Agent' ? 'Disputes team' : 'Customer' }}</small>
            </span>
            <mat-icon class="chevron">expand_more</mat-icon>
          </button>
          <mat-menu #menu="matMenu" xPosition="before">
            <div class="menu-header">
              <strong>{{ user.fullName }}</strong>
              <small>{{ user.email }}</small>
            </div>
            <button mat-menu-item (click)="auth.logout()">
              <mat-icon>logout</mat-icon>
              <span>Sign out</span>
            </button>
          </mat-menu>
        }
      </div>
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
      height: 64px;
      padding: 0 16px;
      background: var(--brand-navy);
      color: #ffffff;
      box-shadow: 0 2px 8px rgb(11 42 91 / 25%);
    }
    .toolbar-inner {
      display: flex;
      align-items: center;
      gap: 24px;
      width: 100%;
      max-width: 1200px;
      height: 100%;
      margin: 0 auto;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      color: inherit;
      text-decoration: none;
    }
    .brand-text {
      display: flex;
      flex-direction: column;
      line-height: 1.1;
    }
    .brand-text strong {
      font-size: 18px;
      letter-spacing: 0.2px;
    }
    .brand-text small,
    .user-text small {
      font-size: 11px;
      font-weight: 400;
      opacity: 0.75;
    }
    .nav {
      display: flex;
      gap: 4px;
      align-self: stretch;
    }
    .nav-link {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 0 14px;
      color: rgb(255 255 255 / 80%);
      text-decoration: none;
      font-size: 14px;
      font-weight: 500;
      border-bottom: 3px solid transparent;
      border-top: 3px solid transparent;
    }
    .nav-link mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .nav-link:hover {
      color: #ffffff;
    }
    .nav-link.active {
      color: #ffffff;
      border-bottom-color: var(--brand-accent);
    }
    .nav-link:focus-visible,
    .user:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: -2px;
    }
    .spacer {
      flex: 1;
    }
    .user {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 6px 8px;
      border: 0;
      border-radius: 10px;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    .user:hover {
      background: rgb(255 255 255 / 10%);
    }
    .user-text {
      display: flex;
      flex-direction: column;
      line-height: 1.2;
      font-size: 13px;
    }
    .avatar {
      display: grid;
      place-items: center;
      width: 34px;
      height: 34px;
      border-radius: 50%;
      background: var(--brand-accent);
      color: #ffffff;
      font-size: 13px;
      font-weight: 700;
    }
    .chevron {
      opacity: 0.75;
    }
    .content {
      flex: 1;
      width: 100%;
      max-width: 1200px;
      margin: 0 auto;
      padding: 28px 16px 48px;
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
      .toolbar {
        padding: 0 8px;
      }
      .toolbar-inner {
        gap: 8px;
      }
      .nav-label,
      .brand-text,
      .user-text,
      .chevron {
        display: none;
      }
      .nav-link {
        padding: 0 10px;
      }
      .content {
        padding: 16px 12px 32px;
      }
    }
  `,
})
export class LayoutComponent {
  protected readonly auth = inject(AuthService);

  protected readonly home = computed(() => this.auth.homeFor(this.auth.user()?.role));

  protected readonly initials = computed(() =>
    (this.auth.user()?.fullName ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join(''),
  );

  protected readonly nav = computed<NavItem[]>(() =>
    this.auth.user()?.role === 'Agent'
      ? [{ path: '/agent/disputes', label: 'Dispute queue', icon: 'inbox' }]
      : [
          { path: '/transactions', label: 'Transactions', icon: 'receipt_long' },
          { path: '/disputes', label: 'My disputes', icon: 'history' },
        ],
  );
}
