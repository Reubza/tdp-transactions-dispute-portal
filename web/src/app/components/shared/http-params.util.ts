import { HttpParams } from '@angular/common/http';

export const API_BASE = '/api/v1';

/** Builds query parameters from an object, skipping empty values. */
export function toHttpParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
