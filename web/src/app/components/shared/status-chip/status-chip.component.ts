import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { statusLabel } from '../dispute-labels.util';
import { DisputeStatus } from '../../../models/dispute.model';

const TONES: Record<DisputeStatus, { tone: string; icon: string }> = {
  Submitted: { tone: 'open', icon: 'schedule' },
  UnderReview: { tone: 'review', icon: 'manage_search' },
  Upheld: { tone: 'good', icon: 'check_circle' },
  Rejected: { tone: 'bad', icon: 'cancel' },
  Withdrawn: { tone: 'neutral', icon: 'undo' },
};

@Component({
  selector: 'app-status-chip',
  imports: [MatIconModule],
  template: `<span [class]="'chip ' + style().tone"
    ><mat-icon aria-hidden="true">{{ style().icon }}</mat-icon
    >{{ label() }}</span
  >`,
  styles: `
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 3px 10px 3px 6px;
      border-radius: 999px;
      white-space: nowrap;
      font: var(--mat-sys-label-medium);
      font-weight: 500;
    }
    mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .open {
      background: #fff1e0;
      color: #8a4b00;
    }
    .review {
      background: #e6edf8;
      color: #17407f;
    }
    .good {
      background: #e3f4e8;
      color: #11592c;
    }
    .bad {
      background: #fde7e8;
      color: #9b1c22;
    }
    .neutral {
      background: #eef0f3;
      color: #4b5563;
    }
  `,
})
export class StatusChipComponent {
  readonly status = input.required<DisputeStatus>();

  protected readonly label = computed(() => statusLabel(this.status()));
  protected readonly style = computed(() => TONES[this.status()] ?? TONES.Withdrawn);
}
