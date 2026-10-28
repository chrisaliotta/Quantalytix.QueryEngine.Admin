import { SplitIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAppNav } from '@/lib/navigation'
import { useReportBridge } from '@/lib/mappings/client'
import { useEditor } from '@/lib/report/store'

/** The report bridge a mapped source writes its row mappings to, with a jump to view it under its dimension. */
export function ReportBridgeLine({ sourceKey }: { sourceKey: string }) {
  const { report } = useEditor()
  const nav = useAppNav()
  const { data: bridge } = useReportBridge(report.settings.key, sourceKey)
  if (!bridge) return null
  const live = bridge.versions.find((v) => v.status === 'published')
  const draft = bridge.versions.find((v) => v.status === 'draft' || v.status === 'validated')

  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
      <SplitIcon aria-hidden className="size-3.5 shrink-0" />
      <span>
        Report bridge <span className="font-mono text-foreground">{bridge.key}</span>
        {' · '}
        {bridge.source.displayName} → {bridge.target.displayName}
        {' · '}
        {live ? `v${live.versionNumber} live, ${live.mappingCount} rows` : 'not published'}
        {draft && ` · v${draft.versionNumber} ${draft.status}`}
      </span>
      <Button
        variant="link"
        size="xs"
        className="h-auto p-0"
        onClick={() => nav.dimension(bridge.targetDimensionId, { tab: 'bridges', br: bridge.id })}
      >
        View bridge
      </Button>
    </div>
  )
}
