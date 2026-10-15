import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { BridgeCrumb } from './BridgeCrumb';

/**
 * Bridges routes, mounted under `/t/:tenantId`: the list and one bridge (`?version=` selects the version). Each page
 * loads on its first visit, as its own chunk.
 */
export const bridgesRoutes: RouteObject[] = [
  {
    path: 'bridges',
    handle: { crumb: 'Bridges' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./BridgeListPage')).BridgeListPage } },
      {
        path: ':bridgeId',
        lazy: { Component: async () => (await import('./BridgeDetailPage')).BridgeDetailPage },
        handle: {
          crumb: match => <BridgeCrumb bridgeId={Number(match.params.bridgeId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
