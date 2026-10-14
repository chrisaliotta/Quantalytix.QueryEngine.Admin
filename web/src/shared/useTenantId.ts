import { useParams } from 'react-router';

/** Tenant id of the current `/t/:tenantId` route; use on tenant routes only. */
export function useTenantId(): string {
  return useParams().tenantId as string;
}
