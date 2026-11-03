import { DisputeReason, DisputeStatus } from './models';

export const DISPUTE_REASONS: { value: DisputeReason; label: string }[] = [
  { value: 'Unauthorised', label: "I didn't make or authorise this transaction" },
  { value: 'Duplicate', label: 'I was charged more than once' },
  { value: 'IncorrectAmount', label: 'The amount is wrong' },
  { value: 'GoodsOrServicesNotReceived', label: "I didn't receive the goods or service" },
  { value: 'CancelledSubscription', label: 'I cancelled this subscription' },
  { value: 'Other', label: 'Something else' },
];

const REASON_SHORT_LABELS: Record<DisputeReason, string> = {
  Unauthorised: 'Unauthorised',
  Duplicate: 'Duplicate charge',
  IncorrectAmount: 'Incorrect amount',
  GoodsOrServicesNotReceived: 'Not received',
  CancelledSubscription: 'Cancelled subscription',
  Other: 'Other',
};

const STATUS_LABELS: Record<DisputeStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  Upheld: 'Resolved – upheld',
  Rejected: 'Resolved – declined',
  Withdrawn: 'Withdrawn',
};

export const DISPUTE_STATUSES = Object.keys(STATUS_LABELS) as DisputeStatus[];

export function reasonLabel(reason: DisputeReason): string {
  return REASON_SHORT_LABELS[reason] ?? reason;
}

export function statusLabel(status: DisputeStatus): string {
  return STATUS_LABELS[status] ?? status;
}

export function isOpenStatus(status: DisputeStatus): boolean {
  return status === 'Submitted' || status === 'UnderReview';
}
