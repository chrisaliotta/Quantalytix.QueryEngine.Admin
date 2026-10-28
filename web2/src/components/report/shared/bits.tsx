import { CalculatorIcon, FolderIcon, HeadingIcon, MinusIcon, SigmaIcon } from 'lucide-react'
import type { ComponentProps } from 'react'
import type { ReportRowKind, Violation } from '@/lib/reports/contract'
import { cn } from '@/lib/utils'

export const KIND_LABEL: Record<ReportRowKind, string> = {
  data: 'Data',
  group: 'Group',
  section: 'Section',
  formula: 'Formula',
}

export function KindGlyph({ kind, className }: { kind: ReportRowKind; className?: string }) {
  const Icon = { data: MinusIcon, group: SigmaIcon, section: HeadingIcon, formula: CalculatorIcon }[kind]
  return (
    <Icon
      aria-label={KIND_LABEL[kind]}
      className={cn('size-3.5 shrink-0', kind === 'data' ? 'text-muted-foreground/60' : 'text-muted-foreground', className)}
    />
  )
}

export function KeyText({ className, ...props }: ComponentProps<'span'>) {
  return <span className={cn('font-mono text-xs text-muted-foreground', className)} {...props} />
}

export function FolderGlyph({ className }: { className?: string }) {
  return <FolderIcon aria-hidden className={cn('size-3.5 shrink-0 text-muted-foreground', className)} />
}

export function StatusDot({ status, className }: { status: 'ok' | 'warn' | 'fail'; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn(
        'inline-block size-2 shrink-0 rounded-full',
        status === 'ok' && 'bg-primary',
        status === 'warn' && 'bg-warning',
        status === 'fail' && 'bg-destructive',
        className,
      )}
    />
  )
}

export function SectionLabel({ className, ...props }: ComponentProps<'h3'>) {
  return <h3 className={cn('text-xs font-medium uppercase tracking-wide text-muted-foreground', className)} {...props} />
}

export const WARN_CHIP = 'border-warning/50 bg-warning-muted text-warning-foreground'

export function factorLabel(factor: number) {
  if (factor === -1) return 'excluded'
  if (factor === 1) return 'x1'
  return `x${factor}`
}

/** The server's refusal of an edit, listed under the control that made it. */
export function Refusal({ violations }: { violations: Violation[] | null }) {
  if (!violations?.length) return null
  return (
    <ul role="alert" className="flex flex-col gap-1 text-xs text-destructive">
      {violations.map((v, i) => (
        <li key={i}>{v.message}</li>
      ))}
    </ul>
  )
}
