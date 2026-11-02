import { createBrowserRouter, Navigate } from 'react-router'
import { paths } from '@/lib/navigation'
import { AppLayout } from './AppLayout'
import { DatasetsPage } from './pages/DatasetsPage'
import { DimensionsPage } from './pages/DimensionsPage'
import { ModelsPage } from './pages/ModelsPage'
import { ReportPage } from './pages/ReportPage'
import { ReportsPage } from './pages/ReportsPage'
import { SnapshotsPage } from './pages/SnapshotsPage'

/**
 * One route per section; the optional id opens an item. Secondary view state (version, tab, sub-editor)
 * lives in the query string through `useSearchParam`.
 */
export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to={paths.reports} replace /> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'reports/:reportId', element: <ReportPage /> },
      { path: 'calculation-models/:modelKey?', element: <ModelsPage /> },
      { path: 'datasets/:datasetId?', element: <DatasetsPage /> },
      { path: 'dimensions/:dimensionId?', element: <DimensionsPage /> },
      { path: 'snapshots/:snapshotId?', element: <SnapshotsPage /> },
      { path: '*', element: <Navigate to={paths.reports} replace /> },
    ],
  },
])
