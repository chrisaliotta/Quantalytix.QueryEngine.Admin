import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { OverviewPage } from './OverviewPage';

/** Overview routes, mounted under `/t/:tenantId`. */
export const overviewRoutes: RouteObject[] = [
  {
    path: 'overview',
    element: <OverviewPage />,
    handle: { crumb: 'Overview' } satisfies RouteHandle,
  },
];
