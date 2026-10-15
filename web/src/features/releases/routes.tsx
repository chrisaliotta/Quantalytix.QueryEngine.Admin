import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { ReleaseCrumb } from './ReleaseCrumb';

/**
 * Releases routes, mounted under `/t/:tenantId`: the release list and the editor of one release. Each page loads on its
 * first visit, as its own chunk.
 */
export const releasesRoutes: RouteObject[] = [
  {
    path: 'releases',
    handle: { crumb: 'Releases' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./ReleasesPage')).ReleasesPage } },
      {
        path: ':releaseId',
        lazy: { Component: async () => (await import('./ReleaseEditorPage')).ReleaseEditorPage },
        handle: {
          crumb: match => <ReleaseCrumb releaseId={Number(match.params.releaseId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
