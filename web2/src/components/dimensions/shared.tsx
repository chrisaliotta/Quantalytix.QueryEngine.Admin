import { CalendarIcon, ListIcon, LockIcon, NetworkIcon, OctagonXIcon, RulerIcon, ShieldCheckIcon, type LucideIcon } from 'lucide-react'
import { useCallback, useState, type ReactElement, type ReactNode } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useDeleteCheck, useRefreshDimensions } from '@/lib/dimensions/client'
import { KIND_LABEL, type ApiResult, type DeleteTarget, type DimensionKind, type EditPolicy, type ProblemDetails, type VersionStatus } from '@/lib/dimensions/contract'
import { cn } from '@/lib/utils'

export const KIND_ICON: Record<DimensionKind, LucideIcon> = { categorical: ListIcon, hierarchical: NetworkIcon, range: RulerIcon, time: CalendarIcon }

export function KindBadge({ kind, className }: { kind: DimensionKind; className?: string }) {
  const Icon = KIND_ICON[kind]
  return (
    <Badge variant="outline" className={cn('gap-1 font-normal', className)}>
      <Icon aria-hidden className="size-3" />
      {KIND_LABEL[kind]}
    </Badge>
  )
}

export const STATUS_TONE: Record<VersionStatus, string> = {
  draft: 'border-warning/50 bg-warning-muted text-warning-foreground',
  validated: 'border-primary/40 bg-primary/10 text-primary',
  published: 'border-success/40 bg-success-muted text-success-foreground',
  retired: 'border-border bg-muted text-muted-foreground',
}

export function StatusBadge({ status, label, className }: { status: VersionStatus; label?: string; className?: string }) {
  return (
    <Badge variant="outline" className={cn('font-mono text-[11px] font-normal', STATUS_TONE[status], className)}>
      {label ?? status}
    </Badge>
  )
}

const POLICY_TONE: Record<EditPolicy, string> = {
  open: 'text-muted-foreground',
  extend: 'border-primary/40 bg-primary/10 text-primary',
  locked: 'border-warning/50 bg-warning-muted text-warning-foreground',
}

export function PolicyBadge({ policy }: { policy: EditPolicy }) {
  return (
    <Badge variant="outline" className={cn('gap-1 font-normal capitalize', POLICY_TONE[policy])}>
      {policy === 'locked' && <LockIcon aria-hidden className="size-3" />}
      {policy}
    </Badge>
  )
}

export function SystemBadge({ name }: { name: string }) {
  return (
    <Badge variant="outline" className="gap-1 font-normal">
      <ShieldCheckIcon aria-hidden className="size-3 text-primary" />
      System · {name}
    </Badge>
  )
}

export function RequiredLock({ what = 'Required' }: { what?: string }) {
  return (
    <span title={`${what} — cannot be deleted or re-keyed`} className="inline-flex text-muted-foreground">
      <LockIcon aria-label={what} className="size-3.5" />
    </span>
  )
}

export function Mono({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={cn('font-mono text-xs text-muted-foreground', className)}>{children}</span>
}

export function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return <th className={cn('h-9 px-3 text-left text-xs font-medium whitespace-nowrap text-muted-foreground', className)}>{children}</th>
}
export function Td({ children, className, colSpan }: { children?: ReactNode; className?: string; colSpan?: number }) {
  return (
    <td colSpan={colSpan} className={cn('px-3 py-2 align-middle', className)}>
      {children}
    </td>
  )
}

export function TabIntro({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <p className="max-w-3xl text-sm text-pretty text-muted-foreground">{children}</p>
      {action}
    </div>
  )
}

export function Field({ label, htmlFor, hint, errors, children, required }: { label: string; htmlFor?: string; hint?: ReactNode; errors?: string[]; children: ReactNode; required?: boolean }) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
        {required && (
          <span aria-hidden className="ml-0.5 text-destructive">
            *
          </span>
        )}
      </label>
      {children}
      {errors?.length ? (
        <ul className="flex flex-col gap-0.5 text-xs text-destructive">
          {errors.map((e) => (
            <li key={e}>{e}</li>
          ))}
        </ul>
      ) : hint ? (
        <p className="text-xs text-pretty text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  )
}

/** Read-only rendering: values as text, never disabled inputs. */
export function ReadValue({ label, children, mono }: { label: string; children: ReactNode; mono?: boolean }) {
  const empty = children === null || children === undefined || children === ''
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className={cn('text-sm break-words', mono && 'font-mono text-xs', empty && 'text-muted-foreground')}>{empty ? '—' : children}</span>
    </div>
  )
}

/** Wraps a control with a tooltip explaining why it's unavailable. */
export function WithReason({ reason, children }: { reason: string | null | undefined; children: ReactElement }) {
  if (!reason) return children
  return (
    <Tooltip>
      <TooltipTrigger render={<span tabIndex={0} className="inline-flex rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring/60" />}>{children}</TooltipTrigger>
      <TooltipContent>{reason}</TooltipContent>
    </Tooltip>
  )
}

export function ProblemBanner({ problem }: { problem: ProblemDetails | null }) {
  if (!problem) return null
  const general = problem.errors['']
  return (
    <div role="alert" className="flex gap-2.5 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm">
      <OctagonXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
      <div className="flex flex-col gap-1">
        <p className="font-medium text-destructive">{problem.title}</p>
        {problem.detail && <p className="text-pretty">{problem.detail}</p>}
        {general?.map((m) => <p key={m}>{m}</p>)}
      </div>
    </div>
  )
}

/** Runs one write: keeps its problem for the form, toasts on success, and revalidates dimension data. */
export function useDimCall() {
  const refresh = useRefreshDimensions()
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

/**
 * Confirms a delete. Fetches blockers first; when blocked it lists them and offers no confirm button.
 */
export function DeleteDialog({
  target,
  title,
  description,
  onOpenChange,
  onConfirm,
}: {
  target: DeleteTarget | null
  title: string
  description?: string
  onOpenChange: (open: boolean) => void
  onConfirm: () => Promise<boolean>
}) {
  const { data: check, isLoading } = useDeleteCheck(target)
  const [busy, setBusy] = useState(false)
  return (
    <Dialog open={!!target} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {isLoading || !check ? (
          <p className="text-sm text-muted-foreground">Checking what depends on it…</p>
        ) : check.allowed ? (
          <p className="text-sm text-pretty">This cannot be undone.</p>
        ) : (
          <div role="alert" className="flex gap-2.5 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm">
            <OctagonXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
            <div className="flex flex-col gap-2">
              <p className="text-pretty">{check.message}</p>
              {check.reasons.length > 1 && (
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

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  children,
  confirmLabel,
  onConfirm,
  destructive,
}: {
  open: boolean
  onOpenChange: (o: boolean) => void
  title: string
  children: ReactNode
  confirmLabel: string
  onConfirm: () => Promise<unknown> | void
  destructive?: boolean
}) {
  const [busy, setBusy] = useState(false)
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <div className="text-sm text-pretty">{children}</div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            disabled={busy}
            onClick={async () => {
              setBusy(true)
              await onConfirm()
              setBusy(false)
            }}
          >
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Right-side editor sheet. Escape and overlay click close it only when the form is clean;
 * a dirty form asks before discarding.
 */
export function EditorSheet({
  open,
  onClose,
  dirty,
  title,
  description,
  children,
  footer,
}: {
  open: boolean
  onClose: () => void
  dirty: boolean
  title: string
  description?: ReactNode
  children: ReactNode
  footer?: ReactNode
}) {
  const [asking, setAsking] = useState(false)
  return (
    <>
      <Sheet
        open={open}
        onOpenChange={(o) => {
          if (o) return
          if (dirty) setAsking(true)
          else onClose()
        }}
      >
        <SheetContent side="right" className="w-full gap-0 sm:max-w-xl">
          <SheetHeader className="border-b">
            <SheetTitle>{title}</SheetTitle>
            {description && <SheetDescription>{description}</SheetDescription>}
          </SheetHeader>
          <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto p-4">{children}</div>
          {footer && <SheetFooter className="flex-row justify-end gap-2 border-t">{footer}</SheetFooter>}
        </SheetContent>
      </Sheet>
      <Dialog open={asking} onOpenChange={setAsking}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Discard changes?</DialogTitle>
            <DialogDescription>Your edits in this sheet haven&apos;t been saved.</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAsking(false)}>
              Keep editing
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                setAsking(false)
                onClose()
              }}
            >
              Discard
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed px-6 py-10 text-center">
      <p className="text-sm font-medium">{title}</p>
      {children && <p className="max-w-md text-sm text-pretty text-muted-foreground">{children}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

export function formatDate(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso.length === 10 ? `${iso}T00:00:00` : iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}
