import { OctagonXIcon } from 'lucide-react'
import { useCallback, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { UnsavedChangesBar } from '@/components/shell/changes'
import type { ComboOption } from '@/components/report/shared/search-combobox'
import { VersionBadge } from '@/components/snapshots/shared'
import type { SnapshotMembership } from '@/lib/api/contract'
import { useRefreshMappings } from '@/lib/mappings/client'
import type { ApiResult, DeleteCheck, MemberRef, Problem, Violation } from '@/lib/mappings/contract'
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

export interface VersionPill extends SnapshotMembership {
  id: string
}

/**
 * The version strip of a mapping set or bridge editor: one pill per snapshot holding it, Go to live on a frozen one, and the
 * editor's save bar.
 */
export function VersionBar({ versions, selected, onSelect }: { versions: VersionPill[]; selected: VersionPill; onSelect: (id: string) => void }) {
  const live = versions.find((v) => v.isLive)!
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card p-2.5">
      <div role="tablist" aria-label="Versions" className="flex flex-wrap items-center gap-1.5">
        {versions.map((v) => (
          <button
            key={v.id}
            type="button"
            role="tab"
            aria-selected={v.id === selected.id}
            onClick={() => onSelect(v.id)}
            className={cn('rounded-full transition-opacity', v.id === selected.id ? 'ring-2 ring-ring/60' : 'opacity-70 hover:opacity-100')}
          >
            <VersionBadge version={v} />
          </button>
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {!selected.isLive && (
          <Button size="sm" variant="ghost" onClick={() => onSelect(live.id)}>
            Go to live
          </Button>
        )}
        <UnsavedChangesBar />
      </div>
    </div>
  )
}

/** The commit rules' findings on the pending content; Save stays blocked until none remains. */
export function FindingsList({ violations, onFilter }: { violations: Violation[]; onFilter: (text: string) => void }) {
  return (
    <section aria-label="Rule findings" className="flex flex-col gap-2 rounded-lg border border-destructive/50 bg-destructive/10 p-3">
      <div className="flex items-center gap-2">
        <OctagonXIcon aria-hidden className="size-4 text-destructive" />
        <h3 className="flex-1 text-sm font-medium">
          {violations.length} rule finding{violations.length === 1 ? '' : 's'} block saving
        </h3>
      </div>
      <ul className="flex flex-col divide-y divide-destructive/20">
        {violations.map((v, i) => (
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
