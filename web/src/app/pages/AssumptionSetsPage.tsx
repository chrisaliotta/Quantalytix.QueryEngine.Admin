import { useParams } from 'react-router'
import { AssumptionSetsScreen } from '@/components/assumption-sets/sets-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'

export function AssumptionSetsPage() {
  const { setId } = useParams()
  return (
    <>
      {!setId && <WorkspaceBar title="Assumption sets" />}
      <AssumptionSetsScreen setId={setId ?? null} />
    </>
  )
}
