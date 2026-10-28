import { PencilIcon } from 'lucide-react'
import { useState } from 'react'
import { cn } from '@/lib/utils'

interface EditableLabelProps {
  value: string
  onCommit: (value: string) => void
  placeholder?: string
  'aria-label': string
  mono?: boolean
  invalid?: boolean
  title?: string
  normalize?: (value: string) => string
  className?: string
  /** Mount already in edit mode, e.g. for a freshly added row. */
  autoEdit?: boolean
  /** Keep the pencil visible at rest so the label reads as editable (titles, headers). */
  showIcon?: boolean
}

/** Reads as plain text; click or focus + Enter swaps in an input. Enter/blur commits, Escape cancels. */
export function EditableLabel({ value, onCommit, placeholder, mono, invalid, title, normalize, className, autoEdit, showIcon, ...rest }: EditableLabelProps) {
  const [editing, setEditing] = useState(!!autoEdit)
  const [draft, setDraft] = useState(value)
  const label = rest['aria-label']

  function commit() {
    setEditing(false)
    if (draft !== value) onCommit(draft.trim())
  }

  if (editing) {
    return (
      <input
        autoFocus
        aria-label={label}
        aria-invalid={invalid || undefined}
        value={draft}
        placeholder={placeholder}
        onChange={(e) => setDraft(normalize ? normalize(e.target.value) : e.target.value)}
        onFocus={(e) => e.currentTarget.select()}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.nativeEvent.isComposing || e.keyCode === 229) return
          if (e.key === 'Enter') {
            e.preventDefault()
            commit()
          } else if (e.key === 'Escape') {
            e.preventDefault()
            setDraft(value)
            setEditing(false)
          }
        }}
        className={cn(
          'h-8 w-full min-w-0 rounded-md border border-ring bg-background px-2 text-sm outline-none ring-2 ring-ring/30 aria-invalid:border-destructive aria-invalid:ring-destructive/30',
          mono && 'font-mono',
          className,
        )}
      />
    )
  }

  return (
    <button
      type="button"
      title={title ?? `Edit ${label.toLowerCase()}`}
      onClick={() => {
        setDraft(value)
        setEditing(true)
      }}
      className={cn(
        'group/label flex h-8 w-full min-w-0 cursor-text items-center gap-1.5 rounded-md border border-transparent px-2 text-left text-sm outline-none hover:border-dashed hover:border-border hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring',
        invalid && 'border-destructive/60 hover:border-destructive',
        mono && 'font-mono',
        className,
      )}
    >
      <span className="sr-only">{label}: </span>
      <span className={cn('truncate', !value && 'text-muted-foreground italic')}>{value || placeholder}</span>
      <PencilIcon
        className={cn(
          'shrink-0 text-muted-foreground group-hover/label:opacity-100 group-focus-visible/label:opacity-100',
          showIcon ? 'size-3.5 opacity-60' : 'ml-auto size-3 opacity-0',
        )}
        aria-hidden
      />
    </button>
  )
}
