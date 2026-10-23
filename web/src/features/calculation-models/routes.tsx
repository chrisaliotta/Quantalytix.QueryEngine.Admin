import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { CalculationModelCrumb } from './CalculationModelCrumb';

/**
 * Calculation model routes, mounted under `/t/:tenantId`: the list and one model. Each page loads on its first visit, as its
 * own chunk.
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
        path: ':calculationModelId',
        lazy: { Component: async () => (await import('./CalculationModelDetailPage')).CalculationModelDetailPage },
        handle: {
          crumb: match => <CalculationModelCrumb calculationModelId={Number(match.params.calculationModelId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
