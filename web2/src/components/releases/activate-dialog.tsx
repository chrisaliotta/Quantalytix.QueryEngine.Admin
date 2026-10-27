import { HistoryIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useLookup, useRelCall, useReleasesApi } from '@/lib/releases/client'
import { DiffList, diffSummary } from './release-tabs'

export function ActivateDialog({ releaseId, onClose }: { releaseId: string; onClose: () => void }) {
  const api = useReleasesApi()
  const lookup = useLookup()
  const { run, saving } = useRelCall()
  const [showList, setShowList] = useState(false)
  const release = lookup.release(releaseId)
  const active = lookup.activeRelease
  const diff = release?.diffVsActive
  const rollback = !!release && !!active && release.createdAt < active.createdAt

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            Activate <span className="font-mono">{release?.key}</span>?
          </DialogTitle>
          <DialogDescription>
            The semantic revision advances, so the query engine reloads its metadata on the next request. Historical assumption sets keep reading the releases they captured.
          </DialogDescription>
        </DialogHeader>
        {rollback && (
          <p className="flex items-center gap-2 rounded-md border border-warning/50 bg-warning-muted p-2.5 text-sm text-warning-foreground">
            <HistoryIcon aria-hidden className="size-4 shrink-0" />
            This rolls back to an older release.
          </p>
        )}
        <div className="flex flex-col gap-2 rounded-md border bg-muted/30 p-3 text-sm">
          {diff ? (
            <>
              <p className="text-pretty">
                Queries and reports switch from <span className="font-mono">{active?.key}</span>: {diffSummary(diff)}.
              </p>
              {diff.added.length + diff.removed.length + diff.changed.length > 0 && (
                <>
                  <button type="button" className="self-start text-xs text-muted-foreground underline-offset-2 hover:text-foreground hover:underline" aria-expanded={showList} onClick={() => setShowList((s) => !s)}>
                    {showList ? 'Hide changes' : 'Show changes'}
                  </button>
                  {showList && <DiffList diff={diff} compact />}
                </>
              )}
            </>
          ) : (
            <p className="text-muted-foreground">Working out what changes…</p>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            disabled={saving}
            onClick={async () => {
              const res = await run(() => api.activateRelease(releaseId), `${release?.key} is now the active release.`)
              if (res.ok) onClose()
            }}
          >
            {saving ? 'Activating…' : 'Activate'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
