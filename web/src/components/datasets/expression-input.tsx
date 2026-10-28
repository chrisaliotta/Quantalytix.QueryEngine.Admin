import { useMemo, useRef, useState } from 'react'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'

export interface CompletionItem {
  key: string
  label: string
}

/** Textarea that completes `[f.` or `[m.` references from the supplied keys. */
export function ExpressionInput({
  id,
  value,
  onChange,
  prefix,
  items,
  placeholder,
  invalid,
  disabled,
  rows = 2,
}: {
  id?: string
  value: string
  onChange: (value: string) => void
  prefix: 'f' | 'm'
  items: CompletionItem[]
  placeholder?: string
  invalid?: boolean
  disabled?: boolean
  rows?: number
}) {
  const ref = useRef<HTMLTextAreaElement>(null)
  const [token, setToken] = useState<{ start: number; query: string } | null>(null)
  const [active, setActive] = useState(0)

  const matches = useMemo(() => {
    if (!token) return []
    const q = token.query.toLowerCase()
    return items.filter((i) => i.key.toLowerCase().includes(q) || i.label.toLowerCase().includes(q)).slice(0, 8)
  }, [items, token])

  function detect(text: string, caret: number) {
    const before = text.slice(0, caret)
    const m = new RegExp(`\\[${prefix}\\.([\\w.-]*)$`).exec(before)
    setToken(m ? { start: caret - m[1].length, query: m[1] } : null)
    setActive(0)
  }

  function accept(item: CompletionItem) {
    const el = ref.current
    if (!el || !token) return
    const caret = el.selectionStart
    const rest = value.slice(caret).replace(/^[\w.-]*\]?/, '')
    const next = `${value.slice(0, token.start)}${item.key}]${rest}`
    onChange(next)
    setToken(null)
    const pos = token.start + item.key.length + 1
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(pos, pos)
    })
  }

  const listId = id ? `${id}-completions` : undefined
  const open = !!token && matches.length > 0

  return (
    <div className="relative">
      <Textarea
        ref={ref}
        id={id}
        rows={rows}
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        aria-invalid={invalid || undefined}
        aria-autocomplete="list"
        aria-controls={open ? listId : undefined}
        aria-expanded={open}
        role="combobox"
        spellCheck={false}
        className="min-h-0 font-mono text-xs"
        onChange={(e) => {
          onChange(e.target.value)
          detect(e.target.value, e.target.selectionStart)
        }}
        onBlur={() => setTimeout(() => setToken(null), 120)}
        onKeyDown={(e) => {
          if (!open) return
          if (e.nativeEvent.isComposing || e.keyCode === 229) return
          if (e.key === 'ArrowDown') {
            e.preventDefault()
            setActive((a) => (a + 1) % matches.length)
          } else if (e.key === 'ArrowUp') {
            e.preventDefault()
            setActive((a) => (a - 1 + matches.length) % matches.length)
          } else if (e.key === 'Enter' || e.key === 'Tab') {
            e.preventDefault()
            accept(matches[active])
          } else if (e.key === 'Escape') {
            setToken(null)
          }
        }}
      />
      {open && (
        <ul id={listId} role="listbox" className="absolute top-full left-0 z-20 mt-1 max-h-56 w-72 overflow-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md">
          {matches.map((m, i) => (
            <li
              key={m.key}
              role="option"
              aria-selected={i === active}
              onMouseDown={(e) => {
                e.preventDefault()
                accept(m)
              }}
              className={cn('flex cursor-pointer items-center justify-between gap-2 rounded-sm px-2 py-1 text-xs', i === active && 'bg-accent text-accent-foreground')}
            >
              <span className="truncate font-mono">
                [{prefix}.{m.key}]
              </span>
              <span className="truncate text-muted-foreground">{m.label}</span>
            </li>
          ))}
        </ul>
      )}
      <p className="mt-1 text-[11px] text-muted-foreground">
        Type <span className="font-mono">[{prefix}.</span> to insert a {prefix === 'f' ? 'field' : 'measure'} reference.
      </p>
    </div>
  )
}
