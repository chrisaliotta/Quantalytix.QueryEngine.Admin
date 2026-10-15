import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { AssumptionSetCrumb } from './AssumptionSetCrumb';

/**
 * Assumption sets routes, mounted under `/t/:tenantId`: the list and the editor of one set. Each page loads on its
 * first visit, as its own chunk.
 */
export const assumptionSetsRoutes: RouteObject[] = [
  {
    path: 'assumption-sets',
    handle: { crumb: 'Assumption sets' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./AssumptionSetsPage')).AssumptionSetsPage } },
      {
        path: ':assumptionSetId',
        lazy: { Component: async () => (await import('./AssumptionSetEditorPage')).AssumptionSetEditorPage },
        handle: {
          crumb: match => <AssumptionSetCrumb assumptionSetId={Number(match.params.assumptionSetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
