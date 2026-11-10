import { UserRole } from './auth.model';

export type DisputeStatus = 'Submitted' | 'UnderReview' | 'Upheld' | 'Rejected' | 'Withdrawn';
export type DisputeReason =
  | 'Unauthorised'
  | 'Duplicate'
  | 'IncorrectAmount'
  | 'GoodsOrServicesNotReceived'
  | 'CancelledSubscription'
  | 'Other';
export type DisputeAction = 'Withdraw' | 'StartReview' | 'Uphold' | 'Reject';
export type AgentDisputeAction = Exclude<DisputeAction, 'Withdraw'>;

export interface DisputeSummary {
  id: string;
  transactionId: string;
  customerName: string;
  merchant: string;
  transactionReference: string;
  amount: number;
  currency: string;
  transactionPostedAt: string;
  reason: DisputeReason;
  status: DisputeStatus;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
}

export interface DisputeHistoryEntry {
  fromStatus: DisputeStatus | null;
  toStatus: DisputeStatus;
  actorRole: UserRole;
  note: string | null;
  changedAt: string;
}

export interface DisputeDetail {
  summary: DisputeSummary;
  description: string;
  resolutionNote: string | null;
  availableActions: DisputeAction[];
  history: DisputeHistoryEntry[];
}

export interface DisputeQuery {
  page: number;
  pageSize: number;
  status?: DisputeStatus;
  open?: boolean;
  search?: string;
}

export interface CreateDisputeRequest {
  transactionId: string;
  reason: DisputeReason;
  description: string;
}
