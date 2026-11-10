import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, toHttpParams } from '../components/shared/http-params.util';
import { PagedResult } from '../models/common.model';
import {
  AgentDisputeAction,
  DisputeDetail,
  DisputeQuery,
  DisputeSummary,
} from '../models/dispute.model';

/** Back-office dispute endpoints used by agents. */
@Injectable({ providedIn: 'root' })
export class AgentDisputesService {
  private readonly http = inject(HttpClient);

  list(query: DisputeQuery): Observable<PagedResult<DisputeSummary>> {
    return this.http.get<PagedResult<DisputeSummary>>(`${API_BASE}/agent/disputes`, {
      params: toHttpParams(query),
    });
  }

  get(id: string): Observable<DisputeDetail> {
    return this.http.get<DisputeDetail>(`${API_BASE}/agent/disputes/${id}`);
  }

  updateStatus(
    id: string,
    action: AgentDisputeAction,
    note: string | null,
  ): Observable<DisputeDetail> {
    return this.http.post<DisputeDetail>(`${API_BASE}/agent/disputes/${id}/status`, {
      action,
      note,
    });
  }
}
