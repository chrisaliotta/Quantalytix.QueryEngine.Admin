import type { RouteObject } from 'react-router';
import { bridgeRoutes } from '@/features/bridges/routes';
import { mappingSetRoutes } from '@/features/mappings/routes';
import type { RouteHandle } from '@/shared/routeHandle';
import { DimensionCrumb } from './DimensionCrumb';

/**
 * Dimensions routes, mounted under `/t/:tenantId`: the list, one dimension (`?version=` selects the version, `?tab=` the tab),
 * and under it the pages of its mapping sets and of the bridges into it. Each page loads on its first visit, as its own chunk;
 * the detail page's tree, band editor and date pickers are large.
 */
export const dimensionsRoutes: RouteObject[] = [
  {
    path: 'dimensions',
    handle: { crumb: 'Dimensions' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./DimensionListPage')).DimensionListPage } },
      {
        path: ':dimensionId',
        handle: {
          crumb: match => <DimensionCrumb dimensionId={Number(match.params.dimensionId)} />,
        } satisfies RouteHandle,
        children: [
          { index: true, lazy: { Component: async () => (await import('./DimensionDetailPage')).DimensionDetailPage } },
          ...mappingSetRoutes,
          ...bridgeRoutes,
        ],
      },
    ],
  },
];
