import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Releases routes, mounted under `/t/:tenantId`. */
export const releasesRoutes: RouteObject[] = [
  {
    path: 'releases',
    element: <PlaceholderPage title="Releases" />,
    handle: { crumb: 'Releases' } satisfies RouteHandle,
  },
];
