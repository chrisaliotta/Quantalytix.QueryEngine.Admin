import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { CalculationModelCrumb } from './CalculationModelCrumb';

/**
 * Calculation model routes, mounted under `/t/:tenantId`: the list and one model (`?version=` selects the version). Each
 * page loads on its first visit, as its own chunk.
 */
export const calculationModelsRoutes: RouteObject[] = [
  {
    path: 'calculation-models',
    handle: { crumb: 'Calculation models' } satisfies RouteHandle,
    children: [
      {
        index: true,
        lazy: { Component: async () => (await import('./CalculationModelListPage')).CalculationModelListPage },
      },
      {
        path: ':modelId',
        lazy: { Component: async () => (await import('./CalculationModelDetailPage')).CalculationModelDetailPage },
        handle: {
          crumb: match => <CalculationModelCrumb modelId={Number(match.params.modelId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
