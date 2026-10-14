import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { ReleaseCrumb } from './ReleaseCrumb';
import { ReleaseEditorPage } from './ReleaseEditorPage';
import { ReleasesPage } from './ReleasesPage';

/** Releases routes, mounted under `/t/:tenantId`: the release list and the editor of one release. */
export const releasesRoutes: RouteObject[] = [
  {
    path: 'releases',
    handle: { crumb: 'Releases' } satisfies RouteHandle,
    children: [
      { index: true, element: <ReleasesPage /> },
      {
        path: ':releaseId',
        element: <ReleaseEditorPage />,
        handle: {
          crumb: match => <ReleaseCrumb releaseId={Number(match.params.releaseId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
