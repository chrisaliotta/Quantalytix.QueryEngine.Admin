import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';

/**
 * System routes, mounted under `/t/:tenantId`; the sidebar shows them to SystemAdmins only, and the API answers 403 to
 * others. The page loads on its first visit, as its own chunk.
 */
export const systemRoutes: RouteObject[] = [
  {
    path: 'system/tenants',
    lazy: { Component: async () => (await import('./TenantsPage')).TenantsPage },
    handle: { crumb: 'Tenants' } satisfies RouteHandle,
  },
];
