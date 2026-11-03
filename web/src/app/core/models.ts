// Mirrors the API contracts in DisputePortal.Application. Enums are serialised as strings.

export type UserRole = 'Customer' | 'Agent';
export type TransactionDirection = 'Debit' | 'Credit';
export type TransactionStatus = 'Pending' | 'Posted' | 'Reversed';
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

export interface UserProfile {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Account {
  id: string;
  name: string;
  maskedAccountNumber: string;
  type: string;
  currency: string;
}

export interface Transaction {
  id: string;
  accountId: string;
  accountName: string;
  maskedAccountNumber: string;
  amount: number;
  currency: string;
  direction: TransactionDirection;
  status: TransactionStatus;
  merchant: string;
  reference: string;
  description: string | null;
  postedAt: string;
  canDispute: boolean;
  notDisputableReason: string | null;
  latestDisputeId: string | null;
  latestDisputeStatus: DisputeStatus | null;
}

export interface TransactionQuery {
  page: number;
  pageSize: number;
  search?: string;
  accountId?: string;
  from?: string;
  to?: string;
  disputed?: boolean;
}

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

/** RFC 9457 problem details as returned by the API. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
