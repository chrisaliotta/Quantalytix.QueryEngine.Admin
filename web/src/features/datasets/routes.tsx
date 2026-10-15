import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { DatasetCrumb } from './DatasetCrumb';

/**
 * Datasets routes, mounted under `/t/:tenantId`: the list, and one dataset with `?version=` selecting its version. Each
 * page loads on its first visit, as its own chunk.
 */
export const datasetsRoutes: RouteObject[] = [
  {
    path: 'datasets',
    handle: { crumb: 'Datasets' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./DatasetListPage')).DatasetListPage } },
      {
        path: ':datasetId',
        lazy: { Component: async () => (await import('./DatasetDetailPage')).DatasetDetailPage },
        handle: {
          crumb: match => <DatasetCrumb datasetId={Number(match.params.datasetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
