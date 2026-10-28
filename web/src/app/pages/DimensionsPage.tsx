import { useParams } from 'react-router'
import { DimensionsScreen } from '@/components/dimensions/dimensions-screen'
import { WorkspaceBar } from '@/components/shell/workspace-bar'

export function DimensionsPage() {
  const { dimensionId } = useParams()
  return (
    <>
      {!dimensionId && <WorkspaceBar title="Dimensions" />}
      <DimensionsScreen dimensionId={dimensionId ?? null} />
    </>
  )
}
