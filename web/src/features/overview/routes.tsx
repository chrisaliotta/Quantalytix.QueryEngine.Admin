import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Overview routes, mounted under `/t/:tenantId`. */
export const overviewRoutes: RouteObject[] = [
  {
    path: 'overview',
    element: <PlaceholderPage title="Overview" />,
    handle: { crumb: 'Overview' } satisfies RouteHandle,
  },
];
