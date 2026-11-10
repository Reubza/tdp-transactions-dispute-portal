import { DisputeStatus } from './dispute.model';

export type TransactionDirection = 'Debit' | 'Credit';
export type TransactionStatus = 'Pending' | 'Posted' | 'Reversed';

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
