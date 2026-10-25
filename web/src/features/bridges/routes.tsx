import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { BridgeCrumb } from './BridgeCrumb';

/**
 * The bridge route, mounted under the route of the dimension a bridge leads into: one analytical bridge (`?version=` selects the
 * version). A report bridge has no page; its report's editor edits it. The page loads on its first visit, as its own chunk.
 */
export const bridgeRoutes: RouteObject[] = [
  {
    path: 'bridges/:bridgeId',
    lazy: { Component: async () => (await import('./BridgeDetailPage')).BridgeDetailPage },
    handle: {
      crumb: match => <BridgeCrumb bridgeId={Number(match.params.bridgeId)} />,
    } satisfies RouteHandle,
  },
];
