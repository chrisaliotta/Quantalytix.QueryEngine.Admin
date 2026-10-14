import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { DatasetCrumb } from './DatasetCrumb';
import { DatasetDetailPage } from './DatasetDetailPage';
import { DatasetListPage } from './DatasetListPage';

/** Datasets routes, mounted under `/t/:tenantId`: the list, and one dataset with `?version=` selecting its version. */
export const datasetsRoutes: RouteObject[] = [
  {
    path: 'datasets',
    handle: { crumb: 'Datasets' } satisfies RouteHandle,
    children: [
      { index: true, element: <DatasetListPage /> },
      {
        path: ':datasetId',
        element: <DatasetDetailPage />,
        handle: {
          crumb: match => <DatasetCrumb datasetId={Number(match.params.datasetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
