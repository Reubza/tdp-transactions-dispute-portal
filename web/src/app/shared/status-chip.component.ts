import { Component, computed, input } from '@angular/core';
import { statusLabel } from '../core/labels';
import { DisputeStatus } from '../core/models';

@Component({
  selector: 'app-status-chip',
  template: `<span [class]="'chip ' + tone()">{{ label() }}</span>`,
  styles: `
    .chip {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 999px;
      white-space: nowrap;
      font: var(--mat-sys-label-medium);
      border: 1px solid transparent;
    }
    .open {
      background: #fff4d6;
      color: #6b4b00;
      border-color: #f1d48a;
    }
    .review {
      background: #e3ecff;
      color: #1d3f8a;
      border-color: #b5c8f5;
    }
    .good {
      background: #ddf5e4;
      color: #11592c;
      border-color: #a7dbb8;
    }
    .bad {
      background: #fde3e3;
      color: #8a1c1c;
      border-color: #f2b1b1;
    }
    .neutral {
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class StatusChipComponent {
  readonly status = input.required<DisputeStatus>();

  protected readonly label = computed(() => statusLabel(this.status()));
  protected readonly tone = computed(() => {
    switch (this.status()) {
      case 'Submitted':
        return 'open';
      case 'UnderReview':
        return 'review';
      case 'Upheld':
        return 'good';
      case 'Rejected':
        return 'bad';
      default:
        return 'neutral';
    }
  });
}
