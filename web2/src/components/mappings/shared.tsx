import { CircleCheckIcon, OctagonXIcon, PinIcon, SnowflakeIcon, Trash2Icon, XIcon } from 'lucide-react'
import { useCallback, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { ConfirmDialog, formatDate, STATUS_TONE, WithReason } from '@/components/dimensions/shared'
import { SAVE_FIRST, UnsavedChangesBar, useHasPendingChanges } from '@/components/shell/changes'
import type { ComboOption } from '@/components/report/shared/search-combobox'
import { useRefreshMappings, useVersionCommands, useVersionDeleteCheck, type VersionKind } from '@/lib/mappings/client'
import { isEditableStatus, liveVersionOf, versionLabel, type ApiResult, type DeleteCheck, type MappingAssumptionPin, type MemberRef, type Problem, type ValidationReport, type VersionStatus } from '@/lib/mappings/contract'
import type { PreviewStatus } from '@/lib/mappings/rules'
import { cn } from '@/lib/utils'

/** Runs one call: keeps its problem for the form, toasts on success, and revalidates mapping data. */
export function useMapCall() {
  const refresh = useRefreshMappings()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const run = useCallback(
    async <T,>(call: () => Promise<ApiResult<T>>, success?: string | ((data: T) => string)): Promise<{ ok: true; data: T } | { ok: false }> => {
      setSaving(true)
      const result = await call()
      setSaving(false)
      if (!result.ok) {
        setProblem(result.problem)
        if (!Object.keys(result.problem.errors).some((k) => k !== '')) toast.error(result.problem.title, { description: result.problem.detail })
        return { ok: false }
      }
      setProblem(null)
      await refresh()
      if (success) toast.success(typeof success === 'function' ? success(result.data) : success)
      return { ok: true, data: result.data }
    },
    [refresh],
  )
  const errorsFor = useCallback((path: string) => problem?.errors[path], [problem])
  return { run, saving, problem, setProblem, errorsFor }
}

export interface VersionPill {
  id: string
  versionNumber: number
  status: VersionStatus
  retiredAt: string | null
  freezeNote: string | null
}

/** Version pills with the historical-pin marker. */
export function VersionPills({ versions, selectedId, pins, onSelect }: { versions: VersionPill[]; selectedId: string; pins: MappingAssumptionPin[]; onSelect: (id: string) => void }) {
  const sorted = [...versions].sort((a, b) => b.versionNumber - a.versionNumber)
  return (
    <div role="tablist" aria-label="Versions" className="flex flex-wrap items-center gap-1.5">
      {sorted.map((v) => {
        const historical = pins.filter((p) => p.versionId === v.id && !p.isActive)
        const selected = v.id === selectedId
        return (
          <button
            key={v.id}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onSelect(v.id)}
            title={historical.length ? `Pinned by ${historical.map((p) => p.assumptionSetDisplayName).join(', ')}` : undefined}
            className={cn(
              'flex items-center gap-1.5 rounded-md border px-2 py-1 font-mono text-xs transition-colors',
              STATUS_TONE[v.status],
              selected ? 'ring-2 ring-ring/60' : 'opacity-80 hover:opacity-100',
            )}
          >
            <span>v{v.versionNumber}</span>
            <span className="font-sans">{versionLabel(v.status)}</span>
            {historical.length > 0 && <PinIcon aria-label="Pinned by an assumption set" className="size-3" />}
          </button>
        )
      })}
    </div>
  )
}

/**
 * The version strip of a mapping set or bridge editor: pills, Check, Freeze on the live version, Go to live and Delete
 * on a frozen one, and the editor's save bar.
 */
export function VersionBar({
  kind,
  versions,
  selected,
  pins,
  onSelect,
  onReport,
}: {
  kind: VersionKind
  versions: VersionPill[]
  selected: VersionPill
  pins: MappingAssumptionPin[]
  onSelect: (id: string) => void
  onReport: (r: ValidationReport) => void
}) {
  const commands = useVersionCommands(kind)
  const mc = useMapCall()
  const dirty = useHasPendingChanges()
  const [freezing, setFreezing] = useState(false)
  const [note, setNote] = useState('')
  const [deleting, setDeleting] = useState(false)
  const { data: deleteCheck } = useVersionDeleteCheck(kind, deleting ? selected.id : null)
  const live = liveVersionOf(versions)
  const isLive = isEditableStatus(selected.status)
  const n = selected.versionNumber

  const check = async () => {
    const r = await mc.run(() => commands.check(selected.id))
    if (r.ok) onReport(r.data)
  }

  const freeze = async () => {
    const r = await mc.run(() => commands.freeze(selected.id, note.trim() || null))
    setFreezing(false)
    if (!r.ok) return
    if (!r.data.live) {
      onReport(r.data.report)
      toast.error(`v${n} can't be frozen yet`, { description: 'Fix the violations listed above.' })
      return
    }
    setNote('')
    onSelect(r.data.live.id)
    toast.success(`Froze v${n}`, { description: `Editing continues in v${r.data.live.versionNumber}.` })
  }

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card p-2.5">
      <VersionPills versions={versions} selectedId={selected.id} pins={pins} onSelect={onSelect} />
      <div className="flex flex-wrap items-center gap-2">
        {!isLive && (
          <span className="text-xs text-muted-foreground">
            Frozen {formatDate(selected.retiredAt)}
            {selected.freezeNote ? ` · ${selected.freezeNote}` : ''}
          </span>
        )}
        <Button size="sm" variant="outline" disabled={mc.saving} onClick={check}>
          Check
        </Button>
        {isLive ? (
          <WithReason reason={dirty ? SAVE_FIRST : null}>
            <Button size="sm" variant="outline" disabled={dirty || mc.saving} onClick={() => setFreezing(true)}>
              <SnowflakeIcon /> Freeze
            </Button>
          </WithReason>
        ) : (
          <>
            <Button size="sm" variant="ghost" onClick={() => onSelect(live.id)}>
              Go to live
            </Button>
            <Button size="sm" variant="ghost" className="text-destructive hover:text-destructive" disabled={dirty} onClick={() => setDeleting(true)}>
              <Trash2Icon /> Delete v{n}
            </Button>
          </>
        )}
        <UnsavedChangesBar />
      </div>
      <ConfirmDialog open={freezing} onOpenChange={setFreezing} title={`Freeze v${n}?`} confirmLabel="Freeze" onConfirm={freeze}>
        <span className="flex flex-col gap-3">
          <span>
            Checks run first; any violation stops the freeze. v{n} then becomes a read-only snapshot that historical assumption sets keep. Editing continues in v{n + 1}, an exact copy that goes
            live immediately, and the active assumption set moves to it.
          </span>
          <Input aria-label="Freeze note" placeholder="Note, e.g. FY26 close (optional)" value={note} onChange={(e) => setNote(e.target.value)} />
        </span>
      </ConfirmDialog>
      <BlockedDeleteDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete frozen v${n}?`}
        description="Its rows are removed with it."
        check={deleteCheck}
        onConfirm={async () => {
          const r = await mc.run(() => commands.remove(selected.id), `Deleted v${n}`)
          if (r.ok) onSelect(live.id)
          return r.ok
        }}
      />
    </div>
  )
}

export function ValidationList({ report, onFilter, onDismiss }: { report: ValidationReport; onFilter: (text: string) => void; onDismiss: () => void }) {
  const ok = !report.violations.length
  return (
    <section aria-label="Check results" className={cn('flex flex-col gap-2 rounded-lg border p-3', ok ? 'border-success/40 bg-success-muted' : 'border-destructive/50 bg-destructive/10')}>
      <div className="flex items-center gap-2">
        {ok ? <CircleCheckIcon aria-hidden className="size-4 text-success-foreground" /> : <OctagonXIcon aria-hidden className="size-4 text-destructive" />}
        <h3 className="flex-1 text-sm font-medium">{ok ? 'No violations.' : `${report.violations.length} violation${report.violations.length === 1 ? '' : 's'}`}</h3>
        <Button variant="ghost" size="icon-sm" aria-label="Dismiss results" onClick={onDismiss}>
          <XIcon />
        </Button>
      </div>
      {!ok && (
        <ul className="flex flex-col divide-y divide-destructive/20">
          {report.violations.map((v, i) => (
            <li key={`${v.code}-${i}`} className="flex items-center justify-between gap-3 py-1.5 text-sm">
              <span className="text-pretty">{v.message}</span>
              {v.target?.filter !== undefined && (
                <Button variant="link" size="sm" className="h-auto shrink-0 p-0" onClick={() => onFilter(v.target!.filter!)}>
                  Show rows
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

/** Confirms a delete after fetching blockers; when blocked it lists them and offers no confirm button. */
export function BlockedDeleteDialog({
  open,
  title,
  description,
  check,
  onOpenChange,
  onConfirm,
}: {
  open: boolean
  title: string
  description: string
  check: DeleteCheck | undefined
  onOpenChange: (open: boolean) => void
  onConfirm: () => Promise<boolean>
}) {
  const [busy, setBusy] = useState(false)
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {!check ? (
          <p className="text-sm text-muted-foreground">Checking what depends on it…</p>
        ) : check.allowed ? (
          <p className="text-sm">This cannot be undone.</p>
        ) : (
          <div role="alert" className="flex gap-2.5 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm">
            <OctagonXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
            <div className="flex flex-col gap-2">
              <p className="text-pretty">{check.message}</p>
              <ul className="flex list-disc flex-col gap-0.5 pl-4 text-xs text-muted-foreground">
                {check.reasons.map((r, i) => (
                  <li key={`${r.code}-${i}`}>{r.message}</li>
                ))}
              </ul>
            </div>
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {check && !check.allowed ? 'Close' : 'Cancel'}
          </Button>
          {check?.allowed && (
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                setBusy(true)
                const ok = await onConfirm()
                setBusy(false)
                if (ok) onOpenChange(false)
              }}
            >
              {busy ? 'Deleting…' : 'Delete'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Member picker options, indented by tree depth with the ancestor path for search. */
export function memberOptions(members: MemberRef[] | null | undefined, { assignableOnly = false } = {}): ComboOption[] {
  if (!members) return []
  const byKey = new Map(members.map((m) => [m.key, m]))
  const ancestry = (m: MemberRef) => {
    const out: string[] = []
    let p = m.parentKey ? byKey.get(m.parentKey) : undefined
    while (p && out.length < 20) {
      out.unshift(p.displayName)
      p = p.parentKey ? byKey.get(p.parentKey) : undefined
    }
    return out
  }
  return members
    .filter((m) => !assignableOnly || m.isAssignable)
    .map((m) => {
      const path = ancestry(m)
      return { value: m.key, label: m.displayName, hint: m.key, depth: path.length, path: path.join(' › ') || undefined, description: m.isAssignable ? undefined : 'Folder' }
    })
}

/** Shows a source key verbatim, with visible markers for spaces and leading zeros. */
export function SourceKey({ value, markers }: { value: string; markers: string[] }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <code className="rounded-sm bg-muted px-1 font-mono text-xs whitespace-pre">{value.replace(/ /g, '·')}</code>
      {markers.map((m) => (
        <span key={m} className="rounded-sm border border-warning/50 px-1 text-[10px] text-warning-foreground">
          {m}
        </span>
      ))}
    </span>
  )
}

/** Assumption sets that pin a version of the object. */
export function PinsList({ pins }: { pins: MappingAssumptionPin[] }) {
  if (!pins.length) return null
  return (
    <div className="flex flex-col gap-1.5 border-t pt-3">
      <h4 className="text-xs font-medium text-muted-foreground">Assumption sets</h4>
      <ul className="flex flex-col gap-1 text-sm">
        {pins.map((p) => (
          <li key={p.assumptionSetId} className="flex items-center justify-between gap-2">
            <span>
              {p.assumptionSetDisplayName}
              {p.isActive && <span className="ml-1.5 text-xs text-muted-foreground">(active)</span>}
            </span>
            <span className="font-mono text-xs text-muted-foreground">v{p.versionNumber}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

export function withoutKey<T>(record: Record<string, T>, key: string): Record<string, T> {
  return Object.fromEntries(Object.entries(record).filter(([k]) => k !== key))
}

export const PREVIEW_TONE: Record<PreviewStatus, string> = {
  new: 'text-success-foreground',
  changed: 'text-primary',
  removed: 'text-destructive',
  unchanged: 'text-muted-foreground',
  error: 'text-destructive',
}

/** Rule findings shown under one row. */
export function RowIssues({ messages }: { messages: string[] }) {
  return messages.map((m) => (
    <span key={m} className="flex items-center gap-1 text-xs text-destructive">
      <OctagonXIcon aria-hidden className="size-3" />
      {m}
    </span>
  ))
}
