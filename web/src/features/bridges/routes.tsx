import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { BridgeCrumb } from './BridgeCrumb';
import { BridgeListPage } from './BridgeListPage';

/**
 * Bridges routes, mounted under `/t/:tenantId`: the list and one bridge (`?version=` selects the version). The detail
 * page loads on first visit.
 */
export const bridgesRoutes: RouteObject[] = [
  {
    path: 'bridges',
    handle: { crumb: 'Bridges' } satisfies RouteHandle,
    children: [
      { index: true, element: <BridgeListPage /> },
      {
        path: ':bridgeId',
        lazy: async () => {
          const { BridgeDetailPage } = await import('./BridgeDetailPage');
          return { Component: BridgeDetailPage };
        },
        handle: {
          crumb: match => <BridgeCrumb bridgeId={Number(match.params.bridgeId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
