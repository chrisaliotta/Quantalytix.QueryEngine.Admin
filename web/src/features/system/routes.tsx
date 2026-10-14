import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** System routes, mounted under `/t/:tenantId`; the sidebar shows them to SystemAdmins only. */
export const systemRoutes: RouteObject[] = [
  {
    path: 'system/tenants',
    element: <PlaceholderPage title="Tenants" />,
    handle: { crumb: 'Tenants' } satisfies RouteHandle,
  },
];
