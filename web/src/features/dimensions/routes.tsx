import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Dimensions routes, mounted under `/t/:tenantId`. */
export const dimensionsRoutes: RouteObject[] = [
  {
    path: 'dimensions',
    element: <PlaceholderPage title="Dimensions" />,
    handle: { crumb: 'Dimensions' } satisfies RouteHandle,
  },
];
