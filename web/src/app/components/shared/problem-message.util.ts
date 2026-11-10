import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../../models/common.model';

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
