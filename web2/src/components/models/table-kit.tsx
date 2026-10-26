import type { KeyboardEvent } from 'react'
import { FORMAT_LABEL } from '@/components/report/columns/column-dialog'
import type { MissingValue, Rounding, RuleViolation } from '@/lib/model/types'
import type { DisplayFormat } from '@/lib/report/types'
import { RuleList } from './refusal-alert'

export const NEW_ROW_ID = '__new__'

export const scrollToRow = (id: string) =>
  requestAnimationFrame(() => document.querySelector(`[data-row-id="${CSS.escape(id)}"]`)?.scrollIntoView({ block: 'nearest' }))

export const rowInputClass =
  'h-8 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm outline-none placeholder:text-muted-foreground/70 focus:border-ring focus:ring-2 focus:ring-ring/30 aria-invalid:border-destructive'

export const FORMAT_OPTIONS = (Object.keys(FORMAT_LABEL) as DisplayFormat[]).map((f) => ({ value: f, label: FORMAT_LABEL[f] }))
export const ROUNDING_OPTIONS: { value: Rounding; label: string }[] = [
  { value: 'half_even', label: 'Half even' },
  { value: 'half_up', label: 'Half up' },
  { value: 'down', label: 'Down' },
]
export const MISSING_OPTIONS: { value: MissingValue; label: string }[] = [
  { value: 'null', label: 'Null' },
  { value: 'zero', label: 'Zero' },
]

const KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_.]*$/

export function metricKeyError(key: string, taken: string[]) {
  if (!key.trim()) return 'Enter a key.'
  if (!KEY_PATTERN.test(key)) return 'Use letters, digits, dots and underscores; start with a letter.'
  if (taken.includes(key)) return 'This key is already used.'
  return null
}

/** Enter submits the pending row, Escape drops it. Ignores IME composition. */
export function pendingRowKeys(onSubmit: () => void, onCancel: () => void) {
  return (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.nativeEvent.isComposing || e.keyCode === 229) return
    if (e.key === 'Enter') {
      e.preventDefault()
      onSubmit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      onCancel()
    }
  }
}

export function PanelRefusal({ violations, onDismiss }: { violations: RuleViolation[]; onDismiss: () => void }) {
  return (
    <div role="alert" className="flex flex-col gap-1.5 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm">
      <div className="flex items-center justify-between gap-2">
        <p className="font-medium text-destructive">The server refused this change</p>
        <button type="button" onClick={onDismiss} className="text-xs text-muted-foreground hover:text-foreground">
          Dismiss
        </button>
      </div>
      <RuleList violations={violations} />
    </div>
  )
}

/** Shared props for the row-based model sections. */
export interface ModelSectionProps<M> {
  model: M
  focusKey: string | null
  onFocus: (key: string | null) => void
  apply: (fn: (d: M) => void) => RuleViolation[] | null
  onAttribution: (key: string) => void
}
