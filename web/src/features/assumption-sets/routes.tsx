import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Assumption sets routes, mounted under `/t/:tenantId`. */
export const assumptionSetsRoutes: RouteObject[] = [
  {
    path: 'assumption-sets',
    element: <PlaceholderPage title="Assumption sets" />,
    handle: { crumb: 'Assumption sets' } satisfies RouteHandle,
  },
];
