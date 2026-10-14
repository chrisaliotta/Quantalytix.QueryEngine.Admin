import type { RouteObject } from 'react-router';
import { PlaceholderPage } from '@/shared/PlaceholderPage';
import type { RouteHandle } from '@/shared/routeHandle';

/** Datasets routes, mounted under `/t/:tenantId`. */
export const datasetsRoutes: RouteObject[] = [
  {
    path: 'datasets',
    element: <PlaceholderPage title="Datasets" />,
    handle: { crumb: 'Datasets' } satisfies RouteHandle,
  },
];
