import { useRef, useState, type ReactNode } from 'react'
import { DAY_PARAMS, FUNCTIONS, metricsOf, readsOf } from '@/lib/model/rules'
import type { CalcModel } from '@/lib/model/types'
import { cn } from '@/lib/utils'

const TOKEN = /(\[m\.[A-Za-z0-9_]*\]?)|(@[A-Za-z][A-Za-z0-9_]*)|('[^']*'?)|\b(CASE|WHEN|THEN|ELSE|END|IN|COALESCE|NULLIF|ABS|POWER|YEARFRAC)\b|([+\-*/(),=<>])/g

export function highlight(formula: string): ReactNode[] {
  const out: ReactNode[] = []
  let last = 0
  for (const m of formula.matchAll(TOKEN)) {
    const i = m.index ?? 0
    if (i > last) out.push(formula.slice(last, i))
    const cls = m[1]
      ? 'text-primary'
      : m[2]
        ? 'text-warning-foreground'
        : m[3]
          ? 'text-foreground italic'
          : m[4]
            ? 'font-medium text-foreground'
            : 'text-muted-foreground'
    out.push(
      <span key={i} className={cls}>
        {m[0]}
      </span>,
    )
    last = i + m[0].length
  }
  if (last < formula.length) out.push(formula.slice(last))
  return out
}

export function FormulaText({ formula, className }: { formula: string; className?: string }) {
  return <code className={cn('font-mono text-xs whitespace-pre-wrap', className)}>{highlight(formula)}</code>
}

interface FormulaEditorProps {
  id: string
  model: CalcModel
  value: string
  onChange: (value: string) => void
  placeholder?: string
  error?: string | null
  /** Extra inputs offered after "[m." e.g. none for attribution. */
  excludeKey?: string
  'aria-describedby'?: string
}

export function FormulaEditor({ id, model, value, onChange, placeholder, error, excludeKey, ...rest }: FormulaEditorProps) {
  const ref = useRef<HTMLTextAreaElement>(null)
  const [caret, setCaret] = useState(0)
  const [focused, setFocused] = useState(false)
  const [active, setActive] = useState(0)

  const before = value.slice(0, caret)
  const metricPartial = /\[m\.([A-Za-z0-9_]*)$/.exec(before)
  const paramPartial = metricPartial ? null : /@([A-Za-z0-9_]*)$/.exec(before)
  const partial = metricPartial ?? paramPartial
  const paramOptions = [
    ...DAY_PARAMS.map((p) => ({ key: p.slice(1), name: p === '@periodDays' ? 'Days in the period' : 'Days in the year' })),
    ...(model.language === '1.2' ? model.parameters.map((p) => ({ key: p.key, name: `${p.name} · ${p.type}` })) : []),
  ]
  const suggestions = metricPartial
    ? metricsOf(model)
        .filter((m) => m.key !== excludeKey && m.key.startsWith(metricPartial[1].toUpperCase()))
        .slice(0, 8)
    : paramPartial
      ? paramOptions.filter((p) => p.key.toLowerCase().startsWith(paramPartial[1].toLowerCase())).slice(0, 8)
      : []
  const open = focused && suggestions.length > 0

  const insert = (key: string) => {
    if (!partial) return
    const start = caret - partial[1].length
    const close = metricPartial ? ']' : ''
    const after = value.slice(caret).replace(metricPartial ? /^[A-Za-z0-9_]*\]?/ : /^[A-Za-z0-9_]*/, '')
    const next = `${value.slice(0, start)}${key}${close}${after}`
    onChange(next)
    const pos = start + key.length + close.length
    requestAnimationFrame(() => {
      ref.current?.setSelectionRange(pos, pos)
      setCaret(pos)
    })
  }

  const reads = readsOf(value)
  const params = [...new Set(value.match(/@[A-Za-z][A-Za-z0-9_]*/g) ?? [])]

  return (
    <div className="flex flex-col gap-2">
      <div className="relative">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 overflow-hidden rounded-md border border-transparent px-3 py-2 font-mono text-sm leading-relaxed break-words whitespace-pre-wrap text-foreground"
        >
          {value ? highlight(value) : <span className="text-muted-foreground">{placeholder}</span>}
          {'\n'}
        </div>
        <textarea
          ref={ref}
          id={id}
          value={value}
          rows={3}
          spellCheck={false}
          role="combobox"
          aria-expanded={open}
          aria-controls={`${id}-suggest`}
          aria-autocomplete="list"
          aria-invalid={!!error}
          aria-describedby={rest['aria-describedby']}
          onChange={(e) => {
            onChange(e.target.value)
            setCaret(e.target.selectionStart)
            setActive(0)
          }}
          onSelect={(e) => setCaret(e.currentTarget.selectionStart)}
          onFocus={() => setFocused(true)}
          onBlur={() => setTimeout(() => setFocused(false), 120)}
          onKeyDown={(e) => {
            if (!open) return
            if (e.key === 'ArrowDown') {
              e.preventDefault()
              setActive((a) => (a + 1) % suggestions.length)
            } else if (e.key === 'ArrowUp') {
              e.preventDefault()
              setActive((a) => (a - 1 + suggestions.length) % suggestions.length)
            } else if ((e.key === 'Enter' || e.key === 'Tab') && !e.nativeEvent.isComposing && e.keyCode !== 229) {
              e.preventDefault()
              insert(suggestions[active].key)
            } else if (e.key === 'Escape') {
              setFocused(false)
            }
          }}
          className={cn(
            'relative w-full resize-y rounded-md border bg-transparent px-3 py-2 font-mono text-sm leading-relaxed break-words text-transparent caret-foreground outline-none selection:bg-primary/25 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50',
            error && 'border-destructive',
          )}
        />
        {open && (
          <ul
            id={`${id}-suggest`}
            role="listbox"
            className="absolute top-full left-0 z-20 mt-1 max-h-56 w-72 overflow-y-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md"
          >
            {suggestions.map((m, i) => (
              <li
                key={m.key}
                role="option"
                aria-selected={i === active}
                onMouseDown={(e) => {
                  e.preventDefault()
                  insert(m.key)
                }}
                className={cn('flex cursor-pointer items-center justify-between gap-3 rounded-sm px-2 py-1.5 text-sm', i === active && 'bg-accent')}
              >
                <span className="font-mono text-xs">{m.key}</span>
                <span className="truncate text-xs text-muted-foreground">{m.name}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
      <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
        <span>Reads</span>
        {reads.length + params.length === 0 && <span>nothing yet</span>}
        {[...reads, ...params].map((r) => (
          <span key={r} className="rounded-sm border bg-muted px-1.5 py-0.5 font-mono text-foreground">
            {r}
          </span>
        ))}
        <span className="ml-auto hidden sm:inline">
          Functions: {FUNCTIONS.filter((f) => model.language === '1.2' || f !== 'YEARFRAC').join(', ')}
          {model.language === '1.2' && model.parameters.length > 0 && ' · type @ for parameters'}
        </span>
      </div>
    </div>
  )
}
