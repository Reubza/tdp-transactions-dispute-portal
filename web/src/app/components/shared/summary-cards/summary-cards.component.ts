import { Component, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { Account } from '../../../models/transaction.model';
import { DisputesService } from '../../../services/disputes.service';

const ACCOUNT_ICONS: Record<string, string> = {
  Cheque: 'account_balance_wallet',
  Savings: 'savings',
  CreditCard: 'credit_card',
};

const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Cheque: 'Cheque account',
  Savings: 'Savings account',
  CreditCard: 'Credit card',
};

/** Overview strip: the customer's accounts plus open and resolved dispute counts. */
@Component({
  selector: 'app-summary-cards',
  imports: [MatIconModule, RouterLink],
  template: `
    <section class="cards" aria-label="Overview">
      @for (account of accounts(); track account.id) {
        <article class="card account">
          <span class="icon"
            ><mat-icon>{{ accountIcon(account.type) }}</mat-icon></span
          >
          <div>
            <span class="label">{{ accountTypeLabel(account.type) }}</span>
            <strong>{{ account.name }}</strong>
            <span class="muted">{{ account.maskedAccountNumber }}</span>
          </div>
        </article>
      }
      <a class="card stat" routerLink="/disputes">
        <span class="icon open"><mat-icon>pending_actions</mat-icon></span>
        <div>
          <span class="label">Open disputes</span>
          <strong class="count">{{ openCount() ?? '–' }}</strong>
        </div>
      </a>
      <a class="card stat" routerLink="/disputes">
        <span class="icon resolved"><mat-icon>task_alt</mat-icon></span>
        <div>
          <span class="label">Closed disputes</span>
          <strong class="count">{{ closedCount() ?? '–' }}</strong>
        </div>
      </a>
    </section>
  `,
  styles: `
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 12px;
      margin-bottom: 24px;
    }
    .card {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--brand-radius);
      background: #ffffff;
      box-shadow: var(--brand-shadow);
      color: inherit;
      text-decoration: none;
      min-width: 0;
    }
    .card div {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .card strong {
      color: var(--brand-navy);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .account {
      background: linear-gradient(135deg, #ffffff 60%, #eef3fb);
    }
    .stat:hover,
    .stat:focus-visible {
      border-color: var(--brand-navy-soft);
      outline: none;
    }
    .label {
      font-size: 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .muted {
      font-size: 12px;
      color: var(--mat-sys-on-surface-variant);
      font-variant-numeric: tabular-nums;
    }
    .count {
      font-size: 24px;
      line-height: 30px;
    }
    .icon {
      display: grid;
      place-items: center;
      flex: 0 0 44px;
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: #e6edf8;
      color: var(--brand-navy-soft);
    }
    .icon.open {
      background: #fff1e0;
      color: #b45309;
    }
    .icon.resolved {
      background: #e3f4e8;
      color: var(--brand-success);
    }
    @media (max-width: 600px) {
      .cards {
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 8px;
        margin-bottom: 16px;
      }
      .card {
        padding: 12px;
        gap: 10px;
      }
      .icon {
        flex-basis: 36px;
        width: 36px;
        height: 36px;
      }
      .account .label {
        display: none;
      }
      .count {
        font-size: 20px;
        line-height: 24px;
      }
    }
  `,
})
export class SummaryCardsComponent implements OnInit {
  private readonly disputesService = inject(DisputesService);
  private readonly destroyRef = inject(DestroyRef);

  readonly accounts = input<Account[]>([]);

  protected readonly openCount = signal<number | null>(null);
  protected readonly closedCount = signal<number | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  /** Reloads the dispute counts, e.g. after a new dispute is raised. */
  refresh(): void {
    const count = (open: boolean) =>
      this.disputesService.list({ page: 1, pageSize: 1, open }).pipe(
        map((page) => page.totalCount),
        catchError(() => of(null)),
      );
    forkJoin([count(true), count(false)])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([open, closed]) => {
        this.openCount.set(open);
        this.closedCount.set(closed);
      });
  }

  protected accountIcon(type: string): string {
    return ACCOUNT_ICONS[type] ?? 'account_balance';
  }

  protected accountTypeLabel(type: string): string {
    return ACCOUNT_TYPE_LABELS[type] ?? type;
  }
}
