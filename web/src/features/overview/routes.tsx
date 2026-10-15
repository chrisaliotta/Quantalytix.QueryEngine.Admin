import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';

/** Overview routes, mounted under `/t/:tenantId`. The page loads on its first visit, as its own chunk. */
export const overviewRoutes: RouteObject[] = [
  {
    path: 'overview',
    lazy: { Component: async () => (await import('./OverviewPage')).OverviewPage },
    handle: { crumb: 'Overview' } satisfies RouteHandle,
  },
];
