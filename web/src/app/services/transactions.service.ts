import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE, toHttpParams } from '../components/shared/http-params.util';
import { PagedResult } from '../models/common.model';
import { Account, Transaction, TransactionQuery } from '../models/transaction.model';

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly http = inject(HttpClient);

  accounts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${API_BASE}/accounts`);
  }

  list(query: TransactionQuery): Observable<PagedResult<Transaction>> {
    return this.http.get<PagedResult<Transaction>>(`${API_BASE}/transactions`, {
      params: toHttpParams(query),
    });
  }
}
