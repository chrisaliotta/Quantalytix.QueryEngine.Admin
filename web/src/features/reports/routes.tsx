import type { RouteObject } from 'react-router';
import type { RouteHandle } from '@/shared/routeHandle';
import { ReportCrumb } from './ReportCrumb';

/**
 * Report routes, mounted under `/t/:tenantId`: the list and one report's editor (`?source=` opens it on a source). Each page
 * loads on its first visit, as its own chunk.
 */
export const reportsRoutes: RouteObject[] = [
  {
    path: 'reports',
    handle: { crumb: 'Reports' } satisfies RouteHandle,
    children: [
      { index: true, lazy: { Component: async () => (await import('./ReportListPage')).ReportListPage } },
      {
        path: ':reportId',
        lazy: { Component: async () => (await import('./ReportDetailPage')).ReportDetailPage },
        handle: {
          crumb: match => <ReportCrumb reportId={Number(match.params.reportId)} />,
        } satisfies RouteHandle,
      },
    ],
  },
];
