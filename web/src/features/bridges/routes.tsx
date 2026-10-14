import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Bridges routes, mounted under `/t/:tenantId`. */
export const bridgesRoutes: RouteObject[] = [
  {
    path: 'bridges',
    element: <PlaceholderPage title="Bridges" />,
    handle: { crumb: 'Bridges' } satisfies RouteHandle,
  },
];
