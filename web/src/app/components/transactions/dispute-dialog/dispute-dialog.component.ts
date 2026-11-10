import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { DisputesService } from '../../../services/disputes.service';
import { problemMessage } from '../../shared/problem-message.util';
import { DISPUTE_REASONS } from '../../shared/dispute-labels.util';
import { DisputeDetail, DisputeReason } from '../../../models/dispute.model';
import { Transaction } from '../../../models/transaction.model';

export const DESCRIPTION_MIN = 10;
export const DESCRIPTION_MAX = 2000;

@Component({
  selector: 'app-dispute-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
    CurrencyPipe,
    DatePipe,
  ],
  template: `
    @if (submitting()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <h2 mat-dialog-title>Dispute transaction</h2>
    <mat-dialog-content>
      <dl class="summary">
        <dt>Merchant</dt>
        <dd>{{ tx.merchant }}</dd>
        <dt>Amount</dt>
        <dd>{{ tx.amount | currency: tx.currency : 'symbol-narrow' }}</dd>
        <dt>Date</dt>
        <dd>{{ tx.postedAt | date: 'd MMM y, HH:mm' }}</dd>
        <dt>Reference</dt>
        <dd>{{ tx.reference }}</dd>
      </dl>

      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }

      <form id="dispute-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <mat-form-field>
          <mat-label>What went wrong?</mat-label>
          <mat-select formControlName="reason" required>
            @for (reason of reasons; track reason.value) {
              <mat-option [value]="reason.value">{{ reason.label }}</mat-option>
            }
          </mat-select>
          @if (form.controls.reason.hasError('required')) {
            <mat-error>Choose a reason</mat-error>
          }
        </mat-form-field>

        <mat-form-field>
          <mat-label>Tell us more</mat-label>
          <textarea
            matInput
            formControlName="description"
            rows="4"
            [maxlength]="descriptionMax"
            placeholder="e.g. I was charged twice for the same order on the same day."
            required
          ></textarea>
          <mat-hint align="end"
            >{{ form.controls.description.value.length }} / {{ descriptionMax }}</mat-hint
          >
          @if (form.controls.description.hasError('required')) {
            <mat-error>Please describe the problem</mat-error>
          } @else if (form.controls.description.hasError('minlength')) {
            <mat-error>Please give at least {{ descriptionMin }} characters</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close [disabled]="submitting()">Cancel</button>
      <button mat-flat-button type="submit" form="dispute-form" [disabled]="submitting()">
        Submit dispute
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    :host {
      display: block;
      max-width: 560px;
    }
    .summary {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 4px 16px;
      margin: 0 0 16px;
    }
    dt {
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0;
      font-weight: 500;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding-top: 8px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class DisputeDialogComponent {
  private readonly disputesService = inject(DisputesService);
  private readonly dialogRef =
    inject<MatDialogRef<DisputeDialogComponent, DisputeDetail>>(MatDialogRef);

  protected readonly tx = inject<Transaction>(MAT_DIALOG_DATA);
  protected readonly reasons = DISPUTE_REASONS;
  protected readonly descriptionMin = DESCRIPTION_MIN;
  protected readonly descriptionMax = DESCRIPTION_MAX;
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  /** One key per dialog, so a retried submission (e.g. after a timeout) can never create two disputes. */
  private readonly idempotencyKey = crypto.randomUUID();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    reason: ['' as DisputeReason | '', Validators.required],
    description: [
      '',
      [
        Validators.required,
        Validators.minLength(DESCRIPTION_MIN),
        Validators.maxLength(DESCRIPTION_MAX),
      ],
    ],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { reason, description } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.dialogRef.disableClose = true;

    this.disputesService
      .create(
        {
          transactionId: this.tx.id,
          reason: reason as DisputeReason,
          description: description.trim(),
        },
        this.idempotencyKey,
      )
      .subscribe({
        next: (dispute) => this.dialogRef.close(dispute),
        error: (err: unknown) => {
          this.submitting.set(false);
          this.dialogRef.disableClose = false;
          this.error.set(
            problemMessage(err, 'We could not submit your dispute. Please try again.'),
          );
        },
      });
  }
}
