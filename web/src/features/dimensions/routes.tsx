import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { DimensionCrumb } from './DimensionCrumb';
import { DimensionListPage } from './DimensionListPage';

/**
 * Dimensions routes, mounted under `/t/:tenantId`: the list and one dimension (`?version=` selects the version). The
 * detail page loads on first visit, because its tree, band editor and date pickers are large.
 */
export const dimensionsRoutes: RouteObject[] = [
  {
    path: 'dimensions',
    handle: { crumb: 'Dimensions' } satisfies RouteHandle,
    children: [
      { index: true, element: <DimensionListPage /> },
      {
        path: ':dimensionId',
        lazy: async () => {
          const { DimensionDetailPage } = await import('./DimensionDetailPage');
          return { Component: DimensionDetailPage };
        },
        handle: {
          crumb: match => <DimensionCrumb dimensionId={Number(match.params.dimensionId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
