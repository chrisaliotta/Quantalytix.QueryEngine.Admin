import { useAppNav } from '@/lib/navigation'
import { useTenantOverview } from '@/lib/tenant/client'

/** What requests that name no snapshot read: the live snapshot at its current semantic revision. Opens the snapshot list. */
export function LiveRevisionChip() {
  const { data: tenant } = useTenantOverview()
  const nav = useAppNav()
  if (!tenant) return null
  return (
    <button
      type="button"
      onClick={nav.snapshots}
      title="Requests that name no snapshot read live. Open snapshots."
      className="inline-flex h-7 items-center gap-1.5 rounded-full border bg-muted/40 px-2.5 font-mono text-xs outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span aria-hidden className="size-1.5 rounded-full bg-success" />
      live
      <span className="text-muted-foreground">· rev {tenant.semanticRevision}</span>
    </button>
  )
}
