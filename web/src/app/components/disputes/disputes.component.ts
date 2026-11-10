import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  Observable,
  of,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { AgentDisputesService } from '../../services/agent-disputes.service';
import { DisputesService } from '../../services/disputes.service';
import { problemMessage } from '../shared/problem-message.util';
import { DISPUTE_STATUSES, reasonLabel, statusLabel } from '../shared/dispute-labels.util';
import { DisputeQuery, DisputeStatus, DisputeSummary } from '../../models/dispute.model';
import { PagedResult } from '../../models/common.model';
import { StatusChipComponent } from '../shared/status-chip/status-chip.component';

type OpenFilter = 'all' | 'open' | 'closed';

interface LoadResult {
  page?: PagedResult<DisputeSummary>;
  err?: unknown;
}

/**
 * Dispute list. In customer mode this is the customer's historic view of their disputes; in agent mode it is
 * the back-office queue across all customers.
 */
@Component({
  selector: 'app-disputes',
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
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressBarModule,
    StatusChipComponent,
  ],
  templateUrl: './disputes.component.html',
  styleUrl: './disputes.component.scss',
})
export class DisputesComponent implements OnInit {
  private readonly disputesService = inject(DisputesService);
  private readonly agentDisputesService = inject(AgentDisputesService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<DisputeQuery>();

  /** Bound from route data. */
  readonly mode = input<'customer' | 'agent'>('customer');

  protected readonly isAgent = computed(() => this.mode() === 'agent');
  protected readonly columns = computed(() =>
    this.isAgent()
      ? ['createdAt', 'customer', 'merchant', 'amount', 'reason', 'status', 'updatedAt']
      : ['createdAt', 'merchant', 'amount', 'reason', 'status', 'updatedAt'],
  );
  protected readonly statuses = DISPUTE_STATUSES;
  protected readonly statusLabel = statusLabel;
  protected readonly reasonLabel = reasonLabel;
  protected readonly result = signal<PagedResult<DisputeSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly paging = signal({ page: 1, pageSize: 20 });

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    open: ['all' as OpenFilter],
    status: ['' as DisputeStatus | ''],
    search: [''],
  });

  ngOnInit(): void {
    // Agents start on the work queue; customers see their full history.
    if (this.isAgent()) this.filters.controls.open.setValue('open', { emitEvent: false });

    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((query) =>
          this.fetch(query).pipe(
            map((page): LoadResult => ({ page })),
            catchError((err: unknown) => of<LoadResult>({ err })),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ page, err }) => {
        this.loading.set(false);
        if (page) this.result.set(page);
        else this.error.set(problemMessage(err, 'We could not load disputes.'));
      });

    this.filters.valueChanges
      .pipe(
        debounceTime(250),
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
      open: f.open === 'all' ? undefined : f.open === 'open',
      status: f.status || undefined,
      search: f.search.trim() || undefined,
    });
  }

  protected onPage(event: PageEvent): void {
    this.paging.set({ page: event.pageIndex + 1, pageSize: event.pageSize });
    this.load();
  }

  protected detailLink(dispute: DisputeSummary): string[] {
    return this.isAgent() ? ['/agent/disputes', dispute.id] : ['/disputes', dispute.id];
  }

  protected open(dispute: DisputeSummary): void {
    void this.router.navigate(this.detailLink(dispute));
  }

  private fetch(query: DisputeQuery): Observable<PagedResult<DisputeSummary>> {
    return this.isAgent()
      ? this.agentDisputesService.list(query)
      : this.disputesService.list(query);
  }
}
