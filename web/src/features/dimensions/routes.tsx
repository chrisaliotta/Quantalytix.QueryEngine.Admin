import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { DimensionCrumb } from './DimensionCrumb';

/**
 * Dimensions routes, mounted under `/t/:tenantId`: the list and one dimension (`?version=` selects the version). Each
 * page loads on its first visit, as its own chunk; the detail page's tree, band editor and date pickers are large.
 */
export const dimensionsRoutes: RouteObject[] = [
  {
    path: 'dimensions',
    handle: { crumb: 'Dimensions' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./DimensionListPage')).DimensionListPage } },
      {
        path: ':dimensionId',
        lazy: { Component: async () => (await import('./DimensionDetailPage')).DimensionDetailPage },
        handle: {
          crumb: match => <DimensionCrumb dimensionId={Number(match.params.dimensionId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
