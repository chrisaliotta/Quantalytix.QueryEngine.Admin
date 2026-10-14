import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { AssumptionSetCrumb } from './AssumptionSetCrumb';
import { AssumptionSetEditorPage } from './AssumptionSetEditorPage';
import { AssumptionSetsPage } from './AssumptionSetsPage';

/** Assumption sets routes, mounted under `/t/:tenantId`: the list and the editor of one set. */
export const assumptionSetsRoutes: RouteObject[] = [
  {
    path: 'assumption-sets',
    handle: { crumb: 'Assumption sets' } satisfies RouteHandle,
    children: [
      { index: true, element: <AssumptionSetsPage /> },
      {
        path: ':assumptionSetId',
        element: <AssumptionSetEditorPage />,
        handle: {
          crumb: match => <AssumptionSetCrumb assumptionSetId={Number(match.params.assumptionSetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
