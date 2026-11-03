import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Account,
  AgentDisputeAction,
  CreateDisputeRequest,
  DisputeDetail,
  DisputeQuery,
  DisputeSummary,
  PagedResult,
  ProblemDetails,
  Transaction,
  TransactionQuery,
} from './models';

const BASE = '/api/v1';

function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

/** Extracts a user-presentable message from an API error. */
export function problemMessage(
  error: unknown,
  fallback = 'Something went wrong. Please try again.',
): string {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    const problem = error.error as ProblemDetails;
    const firstValidationError = problem.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined;
    return firstValidationError ?? problem.detail ?? problem.title ?? fallback;
  }
  return fallback;
}

@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  accounts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${BASE}/accounts`);
  }

  list(query: TransactionQuery): Observable<PagedResult<Transaction>> {
    return this.http.get<PagedResult<Transaction>>(`${BASE}/transactions`, {
      params: toParams(query),
    });
  }
}

@Injectable({ providedIn: 'root' })
export class DisputesApi {
  private readonly http = inject(HttpClient);

  /** `idempotencyKey` makes the request safe to retry: the API returns the original dispute for a repeated key. */
  create(request: CreateDisputeRequest, idempotencyKey: string): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${BASE}/disputes`, request, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }

  list(query: DisputeQuery): Observable<PagedResult<DisputeSummary>> {
    return this.http.get<PagedResult<DisputeSummary>>(`${BASE}/disputes`, {
      params: toParams(query),
    });
  }

  get(id: string): Observable<DisputeDetail> {
    return this.http.get<DisputeDetail>(`${BASE}/disputes/${id}`);
  }

  withdraw(id: string, note: string | null): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${BASE}/disputes/${id}/withdraw`, { note });
  }
}

@Injectable({ providedIn: 'root' })
export class AgentDisputesApi {
  private readonly http = inject(HttpClient);

  list(query: DisputeQuery): Observable<PagedResult<DisputeSummary>> {
    return this.http.get<PagedResult<DisputeSummary>>(`${BASE}/agent/disputes`, {
      params: toParams(query),
    });
  }

  get(id: string): Observable<DisputeDetail> {
    return this.http.get<DisputeDetail>(`${BASE}/agent/disputes/${id}`);
  }

  updateStatus(
    id: string,
    action: AgentDisputeAction,
    note: string | null,
  ): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${BASE}/agent/disputes/${id}/status`, { action, note });
  }
}
