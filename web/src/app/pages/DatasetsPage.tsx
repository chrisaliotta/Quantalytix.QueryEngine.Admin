import { useParams } from 'react-router'
import { DatasetsScreen } from '@/components/datasets/datasets-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'
import { DatasetsApiProvider } from '@/lib/datasets/client'
import { useAppNav } from '@/lib/navigation'

export function DatasetsPage() {
  const { datasetId } = useParams()
  const nav = useAppNav()
  return (
    <DatasetsApiProvider>
      {!datasetId && <WorkspaceBar title="Datasets" />}
      <DatasetsScreen datasetId={datasetId ?? null} onSelect={(id) => (id ? nav.dataset(id) : nav.datasets())} />
    </DatasetsApiProvider>
  )
}
