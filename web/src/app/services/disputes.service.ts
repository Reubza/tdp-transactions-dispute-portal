import { HttpClient, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, toHttpParams } from '../components/shared/http-params.util';
import { PagedResult } from '../models/common.model';
import {
  CreateDisputeRequest,
  DisputeDetail,
  DisputeQuery,
  DisputeSummary,
} from '../models/dispute.model';

/** Customer-facing dispute endpoints. */
@Injectable({ providedIn: 'root' })
export class DisputesService {
  private readonly http = inject(HttpClient);

  /** `idempotencyKey` makes the request safe to retry: the API returns the original dispute for a repeated key. */
  create(request: CreateDisputeRequest, idempotencyKey: string): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${API_BASE}/disputes`, request, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }

  list(query: DisputeQuery): Observable<PagedResult<DisputeSummary>> {
    return this.http.get<PagedResult<DisputeSummary>>(`${API_BASE}/disputes`, {
      params: toHttpParams(query),
    });
  }

  get(id: string): Observable<DisputeDetail> {
    return this.http.get<DisputeDetail>(`${API_BASE}/disputes/${id}`);
  }

  withdraw(id: string, note: string | null): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${API_BASE}/disputes/${id}/withdraw`, { note });
  }
}
