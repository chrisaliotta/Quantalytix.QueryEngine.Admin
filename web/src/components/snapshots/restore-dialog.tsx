import { ConfirmDialog } from '@/components/dimensions/shared'
import { useSnapshotCall, useSnapshotComparison, useSnapshotList, useSnapshotsApi } from '@/lib/snapshots/client'
import type { Snapshot } from '@/lib/snapshots/contract'

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`

/**
 * Confirms making live equal a frozen snapshot, stating what changes in live. The API runs every commit rule on the snapshot's
 * versions first; a refusal lists the violations and changes nothing.
 */
export function RestoreDialog({ snapshot, onClose }: { snapshot: Snapshot | null; onClose: () => void }) {
  return snapshot ? <RestoreConfirm key={snapshot.id} snapshot={snapshot} onClose={onClose} /> : null
}

function RestoreConfirm({ snapshot, onClose }: { snapshot: Snapshot; onClose: () => void }) {
  const api = useSnapshotsApi()
  const { data: list } = useSnapshotList()
  const { data: c } = useSnapshotComparison(list?.live.id ?? null, snapshot.id)
  const { run, problem } = useSnapshotCall()

  return (
    <ConfirmDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`Restore ${snapshot.key} into live?`}
      confirmLabel="Restore"
      destructive
      onConfirm={async () => {
        const res = await run(() => api.restoreSnapshot(snapshot.id), (tenant) => `Live now equals ${snapshot.key} (revision ${tenant.semanticRevision}).`)
        if (res.ok) onClose()
      }}
    >
      <div className="flex flex-col gap-3">
        <p>Live content is replaced by the versions {snapshot.key} holds. Live ids stay; requests and reports that name no snapshot read the restored content at once.</p>
        {c ? (
          <ul className="list-disc pl-5 text-muted-foreground">
            <li>{plural(c.common.length, 'identity')} take the snapshot's content.</li>
            <li>{plural(c.added.length, 'identity')} the snapshot holds and live lacks come back.</li>
            <li>{plural(c.removed.length, 'identity')} added since the capture lose their live version.</li>
          </ul>
        ) : (
          <p className="text-muted-foreground">Comparing with live…</p>
        )}
        <p className="text-muted-foreground">Capture live first if you may want today's state back.</p>
        {problem?.violations?.length ? (
          <div role="alert" className="flex flex-col gap-1.5 rounded-md border border-destructive/40 bg-destructive/5 p-3">
            <span className="font-medium text-destructive">{problem.title}</span>
            <ul className="flex max-h-48 flex-col gap-1 overflow-y-auto text-xs">
              {problem.violations.map((v, i) => (
                <li key={i}>
                  <span className="font-mono text-muted-foreground">{v.code}</span> {v.message}
                </li>
              ))}
            </ul>
          </div>
        ) : null}
      </div>
    </ConfirmDialog>
  )
}

export function deleteSnapshotMessage(key: string) {
  return `The snapshot and every version it holds are deleted, so requests and report comparisons can no longer name ${key}. Identities no other snapshot holds are deleted too. Live does not change.`
}
