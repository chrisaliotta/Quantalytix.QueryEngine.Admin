import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { MappingSetCrumb } from './MappingSetCrumb';
import { MappingSetListPage } from './MappingSetListPage';

/**
 * Mappings routes, mounted under `/t/:tenantId`: the list and one mapping set (`?version=` selects the version). The
 * detail page loads on first visit.
 */
export const mappingsRoutes: RouteObject[] = [
  {
    path: 'mappings',
    handle: { crumb: 'Mappings' } satisfies RouteHandle,
    children: [
      { index: true, element: <MappingSetListPage /> },
      {
        path: ':mappingSetId',
        lazy: async () => {
          const { MappingSetDetailPage } = await import('./MappingSetDetailPage');
          return { Component: MappingSetDetailPage };
        },
        handle: {
          crumb: match => <MappingSetCrumb mappingSetId={Number(match.params.mappingSetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
