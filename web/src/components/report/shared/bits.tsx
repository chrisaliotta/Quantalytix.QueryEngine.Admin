import { CalculatorIcon, FolderIcon, HeadingIcon, MinusIcon, SigmaIcon } from 'lucide-react'
import type { ComponentProps } from 'react'
import type { GroupPresentation, RowKind } from '@/lib/report/types'
import { cn } from '@/lib/utils'

export const KIND_LABEL: Record<RowKind, string> = {
  line: 'Line',
  subtotal: 'Subtotal',
  heading: 'Heading',
  formula: 'Formula',
}

export const PRESENTATION_LABEL: Record<GroupPresentation, string> = {
  total_above: 'Total above',
  total_below: 'Total below',
  header_and_total: 'Header and total',
  step: 'Step',
}

export const PRESENTATION_HINT: Record<GroupPresentation, string> = {
  total_above: 'The total, then the rows under it, indented.',
  total_below: 'The rows under it, indented, then the total.',
  header_and_total: 'A header, the rows under it, indented, then a total line.',
  step: 'The rows under it at the same level, then the total, as in a multi-step income statement.',
}

export const PRESENTATIONS = Object.keys(PRESENTATION_LABEL) as GroupPresentation[]

export function KindGlyph({ kind, className }: { kind: RowKind; className?: string }) {
  const Icon = { line: MinusIcon, subtotal: SigmaIcon, heading: HeadingIcon, formula: CalculatorIcon }[kind]
  return (
    <Icon
      aria-label={KIND_LABEL[kind]}
      className={cn('size-3.5 shrink-0', kind === 'line' ? 'text-muted-foreground/60' : 'text-muted-foreground', className)}
    />
  )
}

export function KeyText({ className, ...props }: ComponentProps<'span'>) {
  return <span className={cn('font-mono text-xs text-muted-foreground', className)} {...props} />
}

export function FolderGlyph({ className }: { className?: string }) {
  return <FolderIcon aria-hidden className={cn('size-3.5 shrink-0 text-muted-foreground', className)} />
}

export function StatusDot({ status, className }: { status: 'pass' | 'warn' | 'fail'; className?: string }) {
  return (
    <span
      aria-hidden
      className={cn(
        'inline-block size-2 shrink-0 rounded-full',
        status === 'pass' && 'bg-primary',
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
