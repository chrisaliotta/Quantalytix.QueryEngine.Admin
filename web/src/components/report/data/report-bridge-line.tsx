import { SplitIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useReportBridge } from '@/lib/mappings/client'
import { useAppNav } from '@/lib/navigation'
import { useReportEditor } from '../editor-state'

/** The report bridge a bridge source writes its rules to, with a jump to view it under its dimension. */
export function ReportBridgeLine({ sourceKey }: { sourceKey: string }) {
  const { reportId } = useReportEditor()
  const nav = useAppNav()
  const { data: bridge } = useReportBridge(reportId, sourceKey)
  if (!bridge) return null

  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
      <SplitIcon aria-hidden className="size-3.5 shrink-0" />
      <span>
        Report bridge <span className="font-mono text-foreground">{bridge.key}</span>
        {' · '}
        {bridge.source.displayName} → {bridge.target.displayName}
        {` · ${bridge.publishedMappingCount} rules`}
      </span>
      <Button variant="link" size="xs" className="h-auto p-0" onClick={() => nav.dimension(bridge.sourceDimensionId, { tab: 'bridges', br: bridge.id })}>
        View bridge
      </Button>
    </div>
  )
}
