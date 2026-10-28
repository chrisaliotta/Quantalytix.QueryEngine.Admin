import { CircleCheckIcon, OctagonXIcon, XIcon } from 'lucide-react'
import type { Violation } from '@/lib/models/contract'
import { cn } from '@/lib/utils'

export function RuleList({ violations }: { violations: Violation[] }) {
  return (
    <ul className="flex flex-col gap-1.5">
      {violations.map((v, i) => (
        <li key={`${v.code}-${i}`} className="flex flex-col gap-0.5 sm:flex-row sm:gap-2">
          <code className="shrink-0 font-mono text-xs font-medium">{v.code}</code>
          <span className="text-pretty">{v.message}</span>
        </li>
      ))}
    </ul>
  )
}

export function RefusalAlert({
  violations,
  title = 'This change was refused',
  onDismiss,
  className,
}: {
  violations: Violation[]
  title?: string
  onDismiss?: () => void
  className?: string
}) {
  return (
    <div role="alert" className={cn('flex gap-3 rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-sm', className)}>
      <OctagonXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <p className="font-medium text-destructive">{title}</p>
        <RuleList violations={violations} />
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Dismiss"
          className="inline-flex size-6 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          <XIcon className="size-4" />
        </button>
      )}
    </div>
  )
}

export function PassAlert({ message, className }: { message: string; className?: string }) {
  return (
    <div role="status" className={cn('flex items-center gap-3 rounded-lg border border-primary/40 bg-primary/10 p-3 text-sm', className)}>
      <CircleCheckIcon aria-hidden className="size-4 shrink-0 text-primary" />
      <p className="font-medium">{message}</p>
    </div>
  )
}
