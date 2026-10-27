import { useReleaseList } from '@/lib/releases/client'
import { useAppNav } from '@/lib/navigation'

export function ActiveReleaseChip() {
  const { data: releases } = useReleaseList()
  const activeRelease = releases?.find((r) => r.isActive)
  const nav = useAppNav()
  if (!activeRelease) return null
  return (
    <button
      type="button"
      onClick={() => nav.release(activeRelease.id)}
      title="The release queries run on. Open it."
      className="inline-flex h-7 items-center gap-1.5 rounded-full border bg-muted/40 px-2.5 font-mono text-xs outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span aria-hidden className="size-1.5 rounded-full bg-success" />
      <span className="sr-only">Active release:</span>
      {activeRelease.key}
    </button>
  )
}
