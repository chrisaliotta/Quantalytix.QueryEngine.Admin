import { QueryClient } from '@tanstack/react-query';
import { ApiProblem } from './problemDetails';

/**
 * App-wide TanStack Query client. API problems are deterministic, so only network failures retry.
 * Tenant-scoped query keys follow `[resource, tenantId, ...]` (see `invalidateTenantQueries`).
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => !(error instanceof ApiProblem) && failureCount < 2,
      refetchOnWindowFocus: false,
    },
  },
});

/** Marks every query of a tenant stale, e.g. after a version status change. */
export function invalidateTenantQueries(tenantId: string) {
  return queryClient.invalidateQueries({ predicate: query => query.queryKey[1] === tenantId });
}
