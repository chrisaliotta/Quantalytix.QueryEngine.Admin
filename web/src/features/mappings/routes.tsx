import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { MappingSetCrumb } from './MappingSetCrumb';

/**
 * The mapping set route, mounted under a dimension's route because a set maps onto that dimension's members: one mapping set
 * (`?version=` selects the version). The page loads on its first visit, as its own chunk.
 */
export const mappingSetRoutes: RouteObject[] = [
  {
    path: 'mapping-sets/:mappingSetId',
    lazy: { Component: async () => (await import('./MappingSetDetailPage')).MappingSetDetailPage },
    handle: {
      crumb: match => <MappingSetCrumb mappingSetId={Number(match.params.mappingSetId)} />,
    } satisfies RouteHandle,
  },
];
