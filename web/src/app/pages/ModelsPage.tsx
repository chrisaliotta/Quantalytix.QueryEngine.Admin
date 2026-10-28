import { useParams } from 'react-router'
import { ModelsScreen } from '@/components/models/models-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'
import { useAppNav } from '@/lib/navigation'

export function ModelsPage() {
  const { modelKey } = useParams()
  const nav = useAppNav()
  return (
    <>
      {!modelKey && <WorkspaceBar title="Calculation models" />}
      <ModelsScreen
        modelKey={modelKey ?? null}
        onSelect={(key) => (key ? nav.model(key) : nav.models())}
        onOpenReport={nav.reportByKey}
      />
    </>
  )
}
