import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AgentDisputesApi, DisputesApi, problemMessage } from '../../core/api.service';
import { reasonLabel, statusLabel } from '../../core/labels';
import {
  AgentDisputeAction,
  DisputeAction,
  DisputeDetail,
  DisputeHistoryEntry,
} from '../../core/models';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { NoteDialogComponent, NoteDialogData } from './note-dialog.component';

const ACTION_DIALOGS: Record<DisputeAction, NoteDialogData> = {
  Withdraw: {
    title: 'Withdraw dispute?',
    message:
      'We will stop investigating this transaction. You can raise a new dispute later if you need to.',
    confirmLabel: 'Withdraw dispute',
    noteLabel: 'Reason (optional)',
    noteRequired: false,
    destructive: true,
  },
  StartReview: {
    title: 'Start review',
    message: 'The customer will see that their dispute is being reviewed.',
    confirmLabel: 'Start review',
    noteLabel: 'Note to customer (optional)',
    noteRequired: false,
  },
  Uphold: {
    title: 'Uphold dispute',
    message: "Resolve this dispute in the customer's favour.",
    confirmLabel: 'Uphold',
    noteLabel: 'Resolution note',
    noteRequired: true,
  },
  Reject: {
    title: 'Decline dispute',
    message:
      'Resolve this dispute against the customer. Explain why so they understand the outcome.',
    confirmLabel: 'Decline',
    noteLabel: 'Resolution note',
    noteRequired: true,
    destructive: true,
  },
};

const ACTION_LABELS: Record<DisputeAction, string> = {
  Withdraw: 'Withdraw dispute',
  StartReview: 'Start review',
  Uphold: 'Uphold',
  Reject: 'Decline',
};

@Component({
  selector: 'app-dispute-detail-page',
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    StatusChipComponent,
  ],
  templateUrl: './dispute-detail.page.html',
  styleUrl: './dispute-detail.page.scss',
})
export class DisputeDetailPage {
  private readonly customerApi = inject(DisputesApi);
  private readonly agentApi = inject(AgentDisputesApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  /** Bound from the route. */
  readonly id = input.required<string>();
  readonly mode = input<'customer' | 'agent'>('customer');

  protected readonly isAgent = computed(() => this.mode() === 'agent');
  protected readonly backLink = computed(() => (this.isAgent() ? '/agent/disputes' : '/disputes'));
  protected readonly dispute = signal<DisputeDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly reasonLabel = reasonLabel;
  protected readonly actionLabel = (a: DisputeAction) => ACTION_LABELS[a];

  /** Newest first, as customers expect in a timeline. */
  protected readonly timeline = computed(() => [...(this.dispute()?.history ?? [])].reverse());

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected load(id: string = this.id()): void {
    this.loading.set(true);
    this.error.set(null);
    const request = this.isAgent() ? this.agentApi.get(id) : this.customerApi.get(id);
    request.subscribe({
      next: (dispute) => {
        this.dispute.set(dispute);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(problemMessage(err, 'We could not load this dispute.'));
      },
    });
  }

  protected act(action: DisputeAction): void {
    this.dialog
      .open(NoteDialogComponent, { data: ACTION_DIALOGS[action], width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((note: string | undefined) => {
        if (note === undefined) return;
        this.busy.set(true);
        this.perform(action, note || null).subscribe({
          next: (updated) => {
            this.busy.set(false);
            this.dispute.set(updated);
            this.snackBar.open(
              `Dispute is now: ${statusLabel(updated.summary.status)}.`,
              undefined,
              { duration: 4000 },
            );
          },
          error: (err: unknown) => {
            this.busy.set(false);
            this.snackBar.open(
              problemMessage(err, 'That action could not be completed.'),
              'Dismiss',
              { duration: 8000 },
            );
            this.load();
          },
        });
      });
  }

  protected entryTitle(entry: DisputeHistoryEntry): string {
    switch (entry.toStatus) {
      case 'Submitted':
        return 'Dispute submitted';
      case 'UnderReview':
        return 'Review started';
      case 'Upheld':
        return this.isAgent() ? 'Dispute upheld' : 'Resolved in your favour';
      case 'Rejected':
        return 'Dispute declined';
      case 'Withdrawn':
        return 'Dispute withdrawn';
    }
  }

  protected actor(entry: DisputeHistoryEntry): string {
    if (entry.actorRole === 'Agent') return 'Disputes team';
    return this.isAgent() ? 'Customer' : 'You';
  }

  private perform(action: DisputeAction, note: string | null): Observable<DisputeDetail> {
    const id = this.id();
    return action === 'Withdraw'
      ? this.customerApi.withdraw(id, note)
      : this.agentApi.updateStatus(id, action as AgentDisputeAction, note);
  }
}
