import { RadioIcon, SnowflakeIcon } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import type { SnapshotMembership } from '@/lib/api/contract'
import { useSnapshotNames } from '@/lib/snapshots/client'
import { cn } from '@/lib/utils'

/** How any module names a version: `live`, or the key of the frozen snapshot that holds it. */
export function useVersionLabel() {
  const names = useSnapshotNames()
  return (v: SnapshotMembership) => (v.isLive ? 'live' : (names.get(v.snapshotId)?.key ?? '…'))
}

/** A version's place: the live version (edited in place) or a frozen snapshot's version (read-only). */
export function VersionBadge({ version, className }: { version: SnapshotMembership; className?: string }) {
  const label = useVersionLabel()(version)
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs whitespace-nowrap',
        version.isLive ? 'border-success/40 bg-success-muted text-success-foreground' : 'text-muted-foreground',
        className,
      )}
    >
      {version.isLive ? <RadioIcon aria-hidden className="size-3" /> : <SnowflakeIcon aria-hidden className="size-3" />}
      <span className={cn(!version.isLive && 'font-mono')}>{version.isLive ? 'Live' : label}</span>
    </span>
  )
}

/** Shown above a frozen version's content: it never changes; restore the snapshot to bring it back into live. */
export function FrozenVersionNote({ version, className }: { version: SnapshotMembership; className?: string }) {
  const label = useVersionLabel()(version)
  return (
    <p role="status" className={cn('flex items-start gap-2 rounded-md border bg-muted/40 p-3 text-sm text-pretty text-muted-foreground', className)}>
      <SnowflakeIcon aria-hidden className="mt-0.5 size-4 shrink-0" />
      <span>
        Read-only: this version belongs to snapshot <span className="font-mono text-foreground">{label}</span>, which never changes. Edits apply to the live version.
      </span>
    </p>
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
function relative(iso: string, now: number) {
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
const shortDate = (iso: string) => new Date(iso).toLocaleDateString('en-CA')

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
  common: 'text-muted-foreground',
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
