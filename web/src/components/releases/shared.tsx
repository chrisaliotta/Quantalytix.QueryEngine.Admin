import { ChevronRightIcon, RadioIcon } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { StatusBadge } from '@/components/dimensions/shared'
import { useAppNav } from '@/lib/navigation'
import type { Lookup } from '@/lib/releases/client'
import type { ReleaseDiff, ReleaseViolationTarget, VersionStatus } from '@/lib/releases/contract'
import { cn } from '@/lib/utils'

export { StatusBadge }

/** Follows a check or readiness target to the page that fixes it. */
export function useOpenTarget(lookup: Lookup, onDataset: (datasetId: string) => void) {
  const nav = useAppNav()
  return (t: ReleaseViolationTarget) => {
    if (t.area === 'dataset') onDataset(t.itemId)
    else if (t.area === 'report') nav.reportByKey(lookup.report(t.itemId)!.key)
    else nav.identity(lookup.identity(t.itemId)!)
  }
}

export function ActiveBadge({ className }: { className?: string }) {
  return (
    <Badge className={cn('gap-1 font-normal', className)}>
      <RadioIcon aria-hidden className="size-3" />
      Active
    </Badge>
  )
}

export function VersionTag({ n, status, onClick, showStatus = true }: { n: number; status: VersionStatus; onClick?: () => void; showStatus?: boolean }) {
  const label = <span className="font-mono text-xs">v{n}</span>
  return (
    <span className="inline-flex items-center gap-1.5">
      {onClick ? (
        <button type="button" onClick={onClick} className="rounded-sm text-foreground underline-offset-2 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring">
          {label}
        </button>
      ) : (
        label
      )}
      {showStatus && <StatusBadge status={status} />}
    </span>
  )
}

export function LinkText({ onClick, children, mono, className }: { onClick: () => void; children: ReactNode; mono?: boolean; className?: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn('rounded-sm text-left text-foreground underline-offset-2 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring', mono && 'font-mono text-xs', className)}
    >
      {children}
    </button>
  )
}

/** Shared clock so relative times re-render together. */
function useNow(intervalMs = 60_000) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(t)
  }, [intervalMs])
  return now
}

const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' })
export function relative(iso: string, now: number) {
  const diff = (new Date(iso).getTime() - now) / 1000
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['week', 604_800],
    ['day', 86_400],
    ['hour', 3600],
    ['minute', 60],
  ]
  for (const [unit, s] of units) if (Math.abs(diff) >= s) return rtf.format(Math.round(diff / s), unit)
  return 'just now'
}
export const absolute = (iso: string) => new Date(iso).toLocaleString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
export const shortDate = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString('en-CA') : '—')

/** Relative in tables, absolute on hover. */
export function RelTime({ iso }: { iso: string | null }) {
  const now = useNow()
  if (!iso) return <span className="text-muted-foreground">—</span>
  return (
    <time dateTime={iso} title={absolute(iso)} className="whitespace-nowrap">
      {relative(iso, now)}
    </time>
  )
}

/** Absolute, relative on hover. */
export function AbsTime({ iso }: { iso: string | null }) {
  const now = useNow()
  if (!iso) return <span className="text-muted-foreground">—</span>
  return (
    <time dateTime={iso} title={relative(iso, now)} className="whitespace-nowrap">
      {shortDate(iso)}
    </time>
  )
}

export const DIFF_TONE = {
  added: 'text-success-foreground',
  removed: 'text-destructive',
  changed: 'text-warning-foreground',
}

export function DiffCounts({ diff, describe }: { diff: ReleaseDiff; describe: (diff: ReleaseDiff) => string[] }) {
  const lines = describe(diff)
  const body = (
    <span className="inline-flex items-center gap-1.5 font-mono text-xs tabular-nums">
      <span className={cn(diff.added.length ? DIFF_TONE.added : 'text-muted-foreground')}>+{diff.added.length}</span>
      <span className={cn(diff.changed.length ? DIFF_TONE.changed : 'text-muted-foreground')}>~{diff.changed.length}</span>
      <span className={cn(diff.removed.length ? DIFF_TONE.removed : 'text-muted-foreground')}>−{diff.removed.length}</span>
    </span>
  )
  if (!lines.length) return body
  return (
    <Tooltip>
      <TooltipTrigger render={<span tabIndex={0} className="rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>{body}</TooltipTrigger>
      <TooltipContent className="max-w-sm">
        <ul className="flex flex-col gap-0.5 font-mono text-[11px]">
          {lines.map((l) => (
            <li key={l}>{l}</li>
          ))}
        </ul>
      </TooltipContent>
    </Tooltip>
  )
}

/** Header row for detail pages: `Section › key` crumb with actions on the right. */
export function DetailBar({ section, onSection, current, children }: { section: string; onSection: () => void; current: string; children?: ReactNode }) {
  return (
    <header className="flex h-12 shrink-0 items-center gap-3 border-b bg-card px-4">
      <nav aria-label="Breadcrumb" className="flex min-w-0 flex-1 items-center gap-1.5 text-sm">
        <button type="button" onClick={onSection} className="rounded-sm text-muted-foreground outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring">
          {section}
        </button>
        <ChevronRightIcon aria-hidden className="size-3.5 text-muted-foreground" />
        <span aria-current="page" className="truncate font-mono text-xs font-medium">
          {current}
        </span>
      </nav>
      {children}
    </header>
  )
}

export function Section({ title, count, children, action }: { title: string; count?: number; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <h2 className="text-sm font-medium">{title}</h2>
        {count !== undefined && <span className="font-mono text-xs text-muted-foreground tabular-nums">{count}</span>}
        <div className="ml-auto">{action}</div>
      </div>
      {children}
    </section>
  )
}

export function useDebounced<T>(value: T, ms = 200) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}

export function FilterChip({ pressed, onPressedChange, children }: { pressed: boolean; onPressedChange: (p: boolean) => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={() => onPressedChange(!pressed)}
      className={cn(
        'h-7 rounded-full border px-2.5 text-xs transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring',
        pressed ? 'border-primary/40 bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground',
      )}
    >
      {children}
    </button>
  )
}
