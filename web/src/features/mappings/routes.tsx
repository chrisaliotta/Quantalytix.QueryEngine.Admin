import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Mappings routes, mounted under `/t/:tenantId`. */
export const mappingsRoutes: RouteObject[] = [
  {
    path: 'mappings',
    element: <PlaceholderPage title="Mappings" />,
    handle: { crumb: 'Mappings' } satisfies RouteHandle,
  },
];
