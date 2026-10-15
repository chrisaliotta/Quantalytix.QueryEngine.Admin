import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { MappingSetCrumb } from './MappingSetCrumb';

/**
 * Mappings routes, mounted under `/t/:tenantId`: the list and one mapping set (`?version=` selects the version). Each
 * page loads on its first visit, as its own chunk.
 */
export const mappingsRoutes: RouteObject[] = [
  {
    path: 'mappings',
    handle: { crumb: 'Mappings' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./MappingSetListPage')).MappingSetListPage } },
      {
        path: ':mappingSetId',
        lazy: { Component: async () => (await import('./MappingSetDetailPage')).MappingSetDetailPage },
        handle: {
          crumb: match => <MappingSetCrumb mappingSetId={Number(match.params.mappingSetId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
