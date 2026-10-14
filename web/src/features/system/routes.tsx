import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { TenantsPage } from './TenantsPage';

/** System routes, mounted under `/t/:tenantId`; the sidebar shows them to SystemAdmins only, and the API answers 403 to others. */
export const systemRoutes: RouteObject[] = [
  {
    path: 'system/tenants',
    element: <TenantsPage />,
    handle: { crumb: 'Tenants' } satisfies RouteHandle,
  },
];
