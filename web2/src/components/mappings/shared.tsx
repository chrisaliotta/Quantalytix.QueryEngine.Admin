import { CircleCheckIcon, OctagonXIcon, PinIcon, XIcon } from 'lucide-react'
import { useCallback, useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { STATUS_TONE } from '@/components/dimensions/shared'
import { useRefreshMappings } from '@/lib/mappings/client'
import type { ApiResult, AssumptionPin, DeleteCheck, MemberRef, ProblemDetails, ValidationReport, VersionStatus } from '@/lib/mappings/contract'
import type { ComboOption } from '@/components/report/shared/search-combobox'
import { cn } from '@/lib/utils'

/** Runs one write: keeps its problem for the form, toasts on success, and revalidates mapping data. */
export function useMapCall() {
  const refresh = useRefreshMappings()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<ProblemDetails | null>(null)
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
}

/** Version pills with the active-assumption marker and any historical pins. */
export function VersionPills({ versions, selectedId, pins, onSelect }: { versions: VersionPill[]; selectedId: string; pins: AssumptionPin[]; onSelect: (id: string) => void }) {
  const sorted = [...versions].sort((a, b) => b.versionNumber - a.versionNumber)
  return (
    <div role="tablist" aria-label="Versions" className="flex flex-wrap items-center gap-1.5">
      {sorted.map((v) => {
        const vPins = pins.filter((p) => p.versionId === v.id)
        const historical = vPins.filter((p) => !p.isActive)
        const selected = v.id === selectedId
        return (
          <button
            key={v.id}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onSelect(v.id)}
            title={historical.length ? `Pinned by ${historical.map((p) => p.assumptionSetName).join(', ')}` : undefined}
            className={cn(
              'flex items-center gap-1.5 rounded-md border px-2 py-1 font-mono text-xs transition-colors',
              STATUS_TONE[v.status],
              selected ? 'ring-2 ring-ring/60' : 'opacity-80 hover:opacity-100',
            )}
          >
            <span>v{v.versionNumber}</span>
            <span className="font-sans">{v.status}</span>
            {historical.length > 0 && <PinIcon aria-label="Pinned by an assumption set" className="size-3" />}
          </button>
        )
      })}
    </div>
  )
}

export function ValidationList({ report, onFilter, onDismiss }: { report: ValidationReport; onFilter: (text: string) => void; onDismiss: () => void }) {
  const ok = !report.violations.length
  return (
    <section aria-label="Validation results" className={cn('flex flex-col gap-2 rounded-lg border p-3', ok ? 'border-success/40 bg-success-muted' : 'border-destructive/50 bg-destructive/10')}>
      <div className="flex items-center gap-2">
        {ok ? <CircleCheckIcon aria-hidden className="size-4 text-success-foreground" /> : <OctagonXIcon aria-hidden className="size-4 text-destructive" />}
        <h3 className="flex-1 text-sm font-medium">{ok ? 'No violations — ready to publish.' : `${report.violations.length} violation${report.violations.length === 1 ? '' : 's'} block publishing`}</h3>
        <Button variant="ghost" size="icon-sm" aria-label="Dismiss results" onClick={onDismiss}>
          <XIcon />
        </Button>
      </div>
      {!ok && (
        <ul className="flex flex-col divide-y divide-destructive/20">
          {report.violations.map((v, i) => (
            <li key={`${v.code}-${i}`} className="flex items-center justify-between gap-3 py-1.5 text-sm">
              <span className="text-pretty">{v.message}</span>
              {v.filter !== undefined && (
                <Button variant="link" size="sm" className="h-auto shrink-0 p-0" onClick={() => onFilter(v.filter!)}>
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
  check,
  onOpenChange,
  onConfirm,
}: {
  open: boolean
  title: string
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
          <DialogDescription>Deleting removes every version and its rows.</DialogDescription>
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
              {check.reasons.length > 0 && (
                <ul className="flex list-disc flex-col gap-0.5 pl-4 text-xs text-muted-foreground">
                  {check.reasons.map((r) => (
                    <li key={r}>{r}</li>
                  ))}
                </ul>
              )}
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
