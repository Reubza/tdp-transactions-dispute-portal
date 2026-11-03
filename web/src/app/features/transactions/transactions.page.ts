import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { problemMessage, TransactionsApi } from '../../core/api.service';
import { Account, PagedResult, Transaction, TransactionQuery } from '../../core/models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { DisputeDialogComponent } from './dispute-dialog.component';

interface LoadResult {
  page?: PagedResult<Transaction>;
  err?: unknown;
}

function toIsoDate(date: Date | null, endOfDay = false): string | undefined {
  if (!date) return undefined;
  const d = new Date(date);
  if (endOfDay) d.setHours(23, 59, 59, 999);
  else d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

@Component({
  selector: 'app-transactions-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    CurrencyPipe,
    DatePipe,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressBarModule,
    StatusChipComponent,
  ],
  templateUrl: './transactions.page.html',
  styleUrl: './transactions.page.scss',
})
export class TransactionsPage implements OnInit {
  private readonly api = inject(TransactionsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<TransactionQuery>();

  protected readonly columns = ['postedAt', 'merchant', 'account', 'amount', 'dispute', 'actions'];
  protected readonly accounts = signal<Account[]>([]);
  protected readonly result = signal<PagedResult<Transaction> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly paging = signal({ page: 1, pageSize: 20 });

  protected readonly filters = inject(FormBuilder).group({
    search: [''],
    accountId: [''],
    from: [null as Date | null],
    to: [null as Date | null],
    disputed: ['' as '' | 'true' | 'false'],
  });

  ngOnInit(): void {
    this.api
      .accounts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (accounts) => this.accounts.set(accounts),
        error: () => this.accounts.set([]),
      });

    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        // Errors are caught per request so one failure doesn't terminate the stream.
        switchMap((query) =>
          this.api.list(query).pipe(
            map((page): LoadResult => ({ page })),
            catchError((err: unknown) => of<LoadResult>({ err })),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ page, err }) => {
        this.loading.set(false);
        if (page) this.result.set(page);
        else this.error.set(problemMessage(err, 'We could not load your transactions.'));
      });

    this.filters.valueChanges
      .pipe(
        debounceTime(300),
        map((v) => JSON.stringify(v)),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.paging.update((p) => ({ ...p, page: 1 }));
        this.load();
      });

    this.load();
  }

  protected load(): void {
    const f = this.filters.getRawValue();
    const { page, pageSize } = this.paging();
    this.reload$.next({
      page,
      pageSize,
      search: f.search?.trim() || undefined,
      accountId: f.accountId || undefined,
      from: toIsoDate(f.from ?? null),
      to: toIsoDate(f.to ?? null, true),
      disputed: f.disputed === '' || f.disputed == null ? undefined : f.disputed === 'true',
    });
  }

  protected onPage(event: PageEvent): void {
    this.paging.set({ page: event.pageIndex + 1, pageSize: event.pageSize });
    this.load();
  }

  protected clearFilters(): void {
    this.filters.reset({ search: '', accountId: '', from: null, to: null, disputed: '' });
  }

  protected openDispute(tx: Transaction): void {
    this.dialog
      .open(DisputeDialogComponent, {
        data: tx,
        autoFocus: 'first-tabbable',
        width: '560px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((dispute) => {
        if (!dispute) return;
        this.load();
        this.snackBar
          .open('Your dispute has been submitted.', 'View', { duration: 8000 })
          .onAction()
          .subscribe(() => void this.router.navigate(['/disputes', dispute.summary.id]));
      });
  }
}
