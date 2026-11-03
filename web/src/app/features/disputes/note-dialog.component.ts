import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface NoteDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  noteLabel: string;
  noteRequired: boolean;
  destructive?: boolean;
}

/** Confirmation dialog that optionally captures a note. Closes with the note (or `''`), or `undefined` on cancel. */
@Component({
  selector: 'app-note-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      <mat-form-field class="full">
        <mat-label>{{ data.noteLabel }}</mat-label>
        <textarea
          matInput
          [formControl]="note"
          rows="3"
          maxlength="1000"
          [required]="data.noteRequired"
        ></textarea>
        @if (note.hasError('required')) {
          <mat-error>A note is required</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button
        mat-flat-button
        type="button"
        [class.destructive]="data.destructive"
        (click)="confirm()"
      >
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
    }
    .destructive {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class NoteDialogComponent {
  protected readonly data = inject<NoteDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<NoteDialogComponent, string>>(MatDialogRef);

  protected readonly note = new FormControl('', {
    nonNullable: true,
    validators: this.data.noteRequired
      ? [Validators.required, Validators.maxLength(1000)]
      : [Validators.maxLength(1000)],
  });

  protected confirm(): void {
    if (this.note.invalid) {
      this.note.markAsTouched();
      return;
    }
    this.dialogRef.close(this.note.value.trim());
  }
}
