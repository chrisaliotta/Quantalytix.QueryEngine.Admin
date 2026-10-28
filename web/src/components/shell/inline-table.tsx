import { PlusIcon, XIcon } from 'lucide-react'
import { useCallback, useEffect, useRef, useState, type FocusEvent, type InputHTMLAttributes, type KeyboardEvent, type ReactNode, type TextareaHTMLAttributes } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { EditableLabel } from '@/components/report/shared/editable-label'
import { SimpleSelect, type Option } from '@/components/report/shared/simple-select'
import { cn } from '@/lib/utils'

/*
 * Inline editing primitives shared by every table in the app.
 * Edits commit through the module's staged API, so they stay dirty until the header batch-save.
 * Text commits on Enter/blur (one undo step per edit, not per keystroke).
 */

const isComposing = (e: KeyboardEvent) => e.nativeEvent.isComposing || e.keyCode === 229

/* ───────────────────────── cells ───────────────────────── */

export const InlineText = EditableLabel

export function InlineNumber({
  value,
  onCommit,
  placeholder = '—',
  className,
  ...rest
}: {
  value: number | null | undefined
  onCommit: (value: number | null) => void
  placeholder?: string
  className?: string
  'aria-label': string
}) {
  return (
    <EditableLabel
      aria-label={rest['aria-label']}
      value={value == null ? '' : String(value)}
      placeholder={placeholder}
      normalize={(v) => v.replace(/[^\d.\-]/g, '')}
      onCommit={(v) => {
        const n = v === '' ? null : Number(v)
        if (n === null || Number.isFinite(n)) onCommit(n)
      }}
      className={cn('justify-end text-right tabular-nums', className)}
    />
  )
}

/** A select that reads as plain text until hovered or focused. */
export function InlineSelect({ value, options, onCommit, placeholder = '—', className, ...rest }: { value: string; options: Option[]; onCommit: (value: string) => void; placeholder?: string; className?: string; 'aria-label': string }) {
  return (
    <SimpleSelect
      size="sm"
      value={value}
      onChange={(v) => v !== value && onCommit(v)}
      options={options}
      placeholder={placeholder}
      aria-label={rest['aria-label']}
      className={cn('border-transparent bg-transparent shadow-none hover:border-border hover:bg-muted/40 dark:bg-transparent', className)}
    />
  )
}

export function InlineSwitch({ checked, onCommit, ...rest }: { checked: boolean; onCommit: (value: boolean) => void; 'aria-label': string }) {
  return <Switch size="sm" checked={checked} onCheckedChange={onCommit} aria-label={rest['aria-label']} />
}

/* ─────────────────── panel inputs (commit on blur) ─────────────────── */

/** Input with a local draft; commits on Enter or blur, Escape reverts. Resyncs when the stored value changes (undo, discard). */
export function CommitInput({ value, onCommit, ...props }: Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> & { value: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value)
  const [synced, setSynced] = useState(value)
  if (value !== synced) {
    setSynced(value)
    setDraft(value)
  }
  return (
    <Input
      {...props}
      value={draft}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={(e) => {
        props.onBlur?.(e)
        if (draft !== value) onCommit(draft)
      }}
      onKeyDown={(e) => {
        props.onKeyDown?.(e)
        if (isComposing(e)) return
        if (e.key === 'Enter') e.currentTarget.blur()
        if (e.key === 'Escape' && draft !== value) {
          e.stopPropagation()
          setDraft(value)
        }
      }}
    />
  )
}

export function CommitTextarea({ value, onCommit, ...props }: Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value' | 'onChange'> & { value: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value)
  const [synced, setSynced] = useState(value)
  if (value !== synced) {
    setSynced(value)
    setDraft(value)
  }
  return (
    <Textarea
      {...props}
      value={draft}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={() => draft !== value && onCommit(draft)}
      onKeyDown={(e) => {
        if (e.key === 'Escape' && draft !== value) {
          e.stopPropagation()
          setDraft(value)
        }
      }}
    />
  )
}

/* ───────────────────────── add row ───────────────────────── */

export interface AddRowField {
  name: string
  placeholder: string
  mono?: boolean
  required?: boolean
  className?: string
  /** Fills the field from the others until the user types into it (e.g. key from name). */
  derive?: (values: Record<string, string>) => string
  normalize?: (value: string) => string
}

export type AddRowResult = Record<string, string[]> | null | void | false

/**
 * The last row of a list or table: type, press Enter, keep typing. Successful adds clear the row and refocus
 * its first field so a list can be entered in one go. Escape clears it (or cancels when already empty).
 */
export function AddRow({
  fields,
  onSubmit,
  variant = 'table',
  label,
  leadingCells = 0,
  trailingCells = 0,
  indent = 0,
  onCancel,
  autoFocus,
  hint,
}: {
  fields: AddRowField[]
  onSubmit: (values: Record<string, string>) => Promise<AddRowResult> | AddRowResult
  variant?: 'table' | 'list'
  /** Accessible name, e.g. "New member". */
  label: string
  leadingCells?: number
  trailingCells?: number
  indent?: number
  onCancel?: () => void
  autoFocus?: boolean
  hint?: ReactNode
}) {
  const empty = () => Object.fromEntries(fields.map((f) => [f.name, '']))
  const [values, setValues] = useState<Record<string, string>>(empty)
  const [touched, setTouched] = useState<Set<string>>(new Set())
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [busy, setBusy] = useState(false)
  const firstRef = useRef<HTMLInputElement>(null)

  const resolved = Object.fromEntries(fields.map((f) => [f.name, !touched.has(f.name) && f.derive && !values[f.name] ? f.derive(values) : values[f.name]]))
  const dirty = fields.some((f) => values[f.name])
  const ready = fields.every((f) => !f.required || resolved[f.name]?.trim())

  function reset() {
    setValues(empty())
    setTouched(new Set())
    setErrors({})
  }

  async function submit() {
    if (!ready || busy) return
    setBusy(true)
    const result = await onSubmit(Object.fromEntries(Object.entries(resolved).map(([k, v]) => [k, v.trim()])))
    setBusy(false)
    if (result === false) return
    if (result && Object.keys(result).length) return setErrors(result)
    reset()
    firstRef.current?.focus()
  }

  function onKey(e: KeyboardEvent<HTMLInputElement>) {
    if (isComposing(e)) return
    if (e.key === 'Enter') {
      e.preventDefault()
      submit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      if (dirty) reset()
      else {
        e.currentTarget.blur()
        onCancel?.()
      }
    }
  }

  const general = errors[''] ?? []
  const errorText = [...general, ...fields.flatMap((f) => errors[f.name] ?? [])]

  const inputs = fields.map((f, i) => (
    <input
      key={f.name}
      ref={i === 0 ? firstRef : undefined}
      autoFocus={autoFocus && i === 0}
      aria-label={`${label}: ${f.placeholder}`}
      aria-invalid={errors[f.name]?.length ? true : undefined}
      value={resolved[f.name]}
      placeholder={f.placeholder}
      disabled={busy}
      onChange={(e) => {
        const v = f.normalize ? f.normalize(e.target.value) : e.target.value
        setValues((s) => ({ ...s, [f.name]: v }))
        setTouched((t) => new Set(t).add(f.name))
        if (errors[f.name] || errors['']) setErrors({})
      }}
      onKeyDown={onKey}
      className={cn(
        'h-8 w-full min-w-0 rounded-md border border-transparent bg-transparent px-2 text-sm outline-none placeholder:text-muted-foreground/70 hover:border-dashed hover:border-border focus:border-ring focus:bg-background focus:ring-2 focus:ring-ring/30 aria-invalid:border-destructive',
        f.mono && 'font-mono text-xs',
        !touched.has(f.name) && f.derive && resolved[f.name] && 'text-muted-foreground',
        f.className,
      )}
    />
  ))

  const submitButton = (
    <Button variant="ghost" size="xs" disabled={!ready || busy} onClick={submit} className={cn(!dirty && 'invisible')}>
      {busy ? 'Adding…' : 'Add'} <kbd className="font-mono text-[10px] text-muted-foreground">↵</kbd>
    </Button>
  )

  if (variant === 'list') {
    return (
      <div role="group" aria-label={label} className="flex flex-col border-t border-dashed">
        <div className="flex h-10 items-center gap-1.5 pr-2" style={{ paddingLeft: `${indent * 20 + 6}px` }}>
          <PlusIcon aria-hidden className="size-3.5 shrink-0 text-muted-foreground" />
          {inputs}
          {submitButton}
        </div>
        {(errorText.length > 0 || (hint && dirty)) && (
          <p role={errorText.length ? 'alert' : undefined} className={cn('pb-2 text-xs', errorText.length ? 'text-destructive' : 'text-muted-foreground')} style={{ paddingLeft: `${indent * 20 + 26}px` }}>
            {errorText.length ? errorText.join(' ') : hint}
          </p>
        )}
      </div>
    )
  }

  return (
    <tr aria-label={label} className="border-t border-dashed bg-muted/10">
      {Array.from({ length: leadingCells }, (_, i) => (
        <td key={`l${i}`} className="px-1 py-1 text-center">
          {i === 0 && <PlusIcon aria-hidden className="mx-auto size-3.5 text-muted-foreground" />}
        </td>
      ))}
      {inputs.map((input, i) => (
        <td key={fields[i].name} className="px-1 py-1 align-top">
          {input}
          {i === 0 && (errorText.length > 0 || (hint && dirty)) && (
            <p role={errorText.length ? 'alert' : undefined} className={cn('px-2 pt-1 text-xs whitespace-normal', errorText.length ? 'text-destructive' : 'text-muted-foreground')}>
              {errorText.length ? errorText.join(' ') : hint}
            </p>
          )}
        </td>
      ))}
      {Array.from({ length: trailingCells }, (_, i) => (
        <td key={`t${i}`} className="px-1 py-1 text-right">
          {i === trailingCells - 1 && submitButton}
        </td>
      ))}
    </tr>
  )
}

/** Upper-snake key suggestion from a display name: "Net interest margin" → NET_INTEREST_MARGIN. */
export function keyFromName(name: string, style: 'upper' | 'lower' = 'upper') {
  const k = name
    .trim()
    .replace(/[^A-Za-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/^(\d)/, 'K_$1')
  return style === 'upper' ? k.toUpperCase() : k.toLowerCase()
}

/* ───────────────────── panel drafts (auto-commit) ───────────────────── */

const isTextEntry = (el: Element | null) => {
  if (!el) return false
  if (el instanceof HTMLTextAreaElement) return true
  if (el instanceof HTMLInputElement) return !['checkbox', 'radio', 'button', 'submit', 'range', 'color'].includes(el.type)
  return (el as HTMLElement).isContentEditable
}

/**
 * Local draft for a properties panel that commits itself into the staged batch: discrete controls
 * (selects, checkboxes, buttons) commit as soon as they change, text commits when it loses focus.
 * Each commit is one staged edit and one undo step. A failed commit keeps the draft so its errors stay visible;
 * external changes to the stored value (undo, discard) resync the draft.
 */
export function usePanelDraft<T>(stored: T, commit: (next: T) => Promise<boolean>) {
  const storedKey = JSON.stringify(stored)
  const [draft, setDraftState] = useState(stored)
  const [synced, setSynced] = useState(storedKey)
  const latest = useRef(draft)
  const storedRef = useRef(storedKey)
  const inflight = useRef(new Set<string>())
  storedRef.current = storedKey

  if (storedKey !== synced) {
    setSynced(storedKey)
    if (!inflight.current.has(storedKey)) {
      latest.current = stored
      setDraftState(stored)
    }
  }
  useEffect(() => {
    inflight.current.delete(storedKey)
  }, [storedKey])

  const flush = useCallback(() => {
    const next = latest.current
    const key = JSON.stringify(next)
    if (key === storedRef.current || inflight.current.has(key)) return
    inflight.current.add(key)
    void commit(next).then((ok) => {
      if (!ok) inflight.current.delete(key)
    })
  }, [commit])

  const setDraft = useCallback((update: T | ((d: T) => T)) => {
    const next = typeof update === 'function' ? (update as (d: T) => T)(latest.current) : update
    latest.current = next
    setDraftState(next)
  }, [])

  useEffect(() => {
    if (!isTextEntry(document.activeElement)) flush()
  }, [draft, flush])

  return {
    draft,
    setDraft,
    flush,
    pending: JSON.stringify(draft) !== storedKey,
    /** Spread on the panel body: text inputs commit when focus leaves them. */
    bodyProps: { onBlur: (e: FocusEvent<HTMLElement>) => isTextEntry(e.target) && flush() },
  }
}

/* ───────────────────── properties panel ───────────────────── */

/** Table + docked properties panel for the focused row. The panel stacks below on narrow screens. */
export function SplitView({ children, panel }: { children: ReactNode; panel?: ReactNode }) {
  return (
    <div className="flex flex-col gap-4 xl:flex-row xl:items-start">
      <div className="flex min-w-0 flex-1 flex-col gap-2">{children}</div>
      {panel}
    </div>
  )
}

export function PropertiesPanel({
  title,
  subtitle,
  onClose,
  actions,
  children,
  label = 'Properties',
  size = 'md',
  bodyProps,
}: {
  title: ReactNode
  subtitle?: ReactNode
  onClose: () => void
  actions?: ReactNode
  children: ReactNode
  label?: string
  /** `lg` for rich editors (datasets): wider, and its body is a container so forms can go two-column. */
  size?: 'md' | 'lg'
  bodyProps?: { onBlur?: (e: FocusEvent<HTMLElement>) => void }
}) {
  return (
    <aside
      aria-label={label}
      onKeyDown={(e) => {
        if (e.key === 'Escape' && !e.defaultPrevented && !(e.target as HTMLElement).closest('[role="listbox"],[role="dialog"]')) onClose()
      }}
      className={cn(
        'flex w-full shrink-0 flex-col overflow-hidden rounded-lg border bg-card xl:sticky xl:top-4 xl:max-h-[calc(100dvh-9rem)]',
        size === 'lg' ? 'xl:w-[34rem]' : 'xl:w-96',
      )}
    >
      <header className="flex items-start gap-2 border-b px-4 py-3">
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <h3 className="truncate text-sm font-medium">{title}</h3>
          {subtitle && <div className="truncate text-xs text-muted-foreground">{subtitle}</div>}
        </div>
        {actions}
        <Button variant="ghost" size="icon-xs" aria-label="Close properties" onClick={onClose}>
          <XIcon />
        </Button>
      </header>
      <div {...bodyProps} className="@container flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-4">
        {children}
      </div>
    </aside>
  )
}

export function PanelSection({ title, children, action, description }: { title: string; children: ReactNode; action?: ReactNode; description?: ReactNode }) {
  return (
    <section className="flex flex-col gap-3 border-t pt-4 first:border-t-0 first:pt-0">
      <div className="flex items-start justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h4 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{title}</h4>
          {description && <p className="text-xs text-pretty text-muted-foreground">{description}</p>}
        </div>
        {action}
      </div>
      {children}
    </section>
  )
}

/** Row classes for a table row that can be focused into the properties panel. */
export const focusRowClass = (focused: boolean) => cn('group cursor-default hover:bg-muted/40', focused && 'bg-primary/5 hover:bg-primary/10 shadow-[inset_0_0_0_1px_var(--primary),inset_3px_0_0_var(--primary)]')
