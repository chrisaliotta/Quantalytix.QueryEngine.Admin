import { createBrowserRouter, Navigate } from 'react-router';
import { LoginPage } from '@/auth/LoginPage';
import { RequireSession } from '@/auth/RequireSession';
import { assumptionSetsRoutes } from '@/features/assumption-sets/routes';
import { bridgesRoutes } from '@/features/bridges/routes';
import { datasetsRoutes } from '@/features/datasets/routes';
import { dimensionsRoutes } from '@/features/dimensions/routes';
import { mappingsRoutes } from '@/features/mappings/routes';
import { overviewRoutes } from '@/features/overview/routes';
import { releasesRoutes } from '@/features/releases/routes';
import { systemRoutes } from '@/features/system/routes';
import { AppShell } from './AppShell';
import { NotFoundPage, RouteErrorPage } from './errorPages';
import { TenantRedirect } from './TenantRedirect';

/**
 * /login, `/` (redirects to a tenant) and `/t/:tenantId`, which renders the AppShell around the
 * routes of every feature. Errors of a feature page render inside the shell.
 */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireSession />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <TenantRedirect /> },
      {
        path: 't/:tenantId',
        element: <AppShell />,
        children: [
          {
            errorElement: <RouteErrorPage />,
            children: [
              { index: true, element: <Navigate to="overview" replace /> },
              ...overviewRoutes,
              ...datasetsRoutes,
              ...dimensionsRoutes,
              ...mappingsRoutes,
              ...bridgesRoutes,
              ...releasesRoutes,
              ...assumptionSetsRoutes,
              ...systemRoutes,
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
