import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { problemMessage } from '../shared/problem-message.util';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  template: `
    <div class="page">
      <mat-card class="card" appearance="outlined">
        @if (submitting()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <mat-card-header>
          <mat-icon mat-card-avatar class="logo">account_balance</mat-icon>
          <mat-card-title>Transactions Dispute Portal</mat-card-title>
          <mat-card-subtitle>Sign in to view and dispute your transactions</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          @if (expired()) {
            <p class="notice" role="status">Your session has ended. Please sign in again.</p>
          }
          @if (error(); as message) {
            <p class="error" role="alert">{{ message }}</p>
          }
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <mat-form-field>
              <mat-label>E-mail address</mat-label>
              <input
                matInput
                type="email"
                formControlName="email"
                autocomplete="username"
                required
              />
              @if (form.controls.email.hasError('email')) {
                <mat-error>Enter a valid e-mail address</mat-error>
              } @else if (form.controls.email.hasError('required')) {
                <mat-error>E-mail address is required</mat-error>
              }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Password</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                autocomplete="current-password"
                required
              />
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="showPassword.set(!showPassword())"
                [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
              >
                <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              @if (form.controls.password.hasError('required')) {
                <mat-error>Password is required</mat-error>
              }
            </mat-form-field>
            <button mat-flat-button type="submit" [disabled]="submitting()">Sign in</button>
          </form>
        </mat-card-content>
      </mat-card>
      <aside class="demo" aria-label="Demo accounts">
        <strong>Demo accounts</strong> (password from the README):
        <ul>
          <li>
            <button type="button" class="link" (click)="fill('thandi@example.com')">
              thandi&#64;example.com
            </button>
            – customer
          </li>
          <li>
            <button type="button" class="link" (click)="fill('james@example.com')">
              james&#64;example.com
            </button>
            – customer
          </li>
          <li>
            <button type="button" class="link" (click)="fill('agent@example.com')">
              agent&#64;example.com
            </button>
            – dispute agent
          </li>
        </ul>
      </aside>
    </div>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 16px;
      gap: 16px;
      box-sizing: border-box;
      background: var(--mat-sys-surface-container-low);
    }
    .card {
      width: 100%;
      max-width: 420px;
      overflow: hidden;
    }
    .logo {
      color: var(--mat-sys-primary);
      font-size: 40px;
      width: 40px;
      height: 40px;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-top: 16px;
    }
    .error {
      color: var(--mat-sys-error);
      margin: 16px 0 0;
    }
    .notice {
      color: var(--mat-sys-on-surface-variant);
      margin: 16px 0 0;
    }
    .demo {
      max-width: 420px;
      width: 100%;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .demo ul {
      margin: 4px 0 0;
      padding-left: 20px;
    }
    .link {
      background: none;
      border: 0;
      padding: 0;
      color: var(--mat-sys-primary);
      cursor: pointer;
      text-decoration: underline;
      font: inherit;
    }
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from the query string. */
  readonly returnUrl = input<string>();
  readonly expired = input<string>();

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected fill(email: string): void {
    this.form.controls.email.setValue(email);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: (response) => {
        const target = this.safeReturnUrl() ?? this.auth.homeFor(response.user.role);
        void this.router.navigateByUrl(target);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(problemMessage(err, 'Unable to sign in. Please try again.'));
      },
    });
  }

  /** Only allow same-app relative paths to prevent open redirects. */
  private safeReturnUrl(): string | null {
    const url = this.returnUrl();
    return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login')
      ? url
      : null;
  }
}
