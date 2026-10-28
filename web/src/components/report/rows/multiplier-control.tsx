import { useId, useState } from 'react'
import { Input } from '@/components/ui/input'
import { mappingMultiplier } from '@/lib/report/coverage'
import { useEditor } from '@/lib/report/store'
import type { Mapping } from '@/lib/report/types'
import { cn } from '@/lib/utils'

export function formatMultiplier(value: number) {
  return `×${Number(value.toPrecision(10))}`
}

function parseMultiplier(raw: string): { value?: number; error?: string } {
  const text = raw.trim()
  if (!text) return { value: 1 }
  const value = Number(text)
  if (!Number.isFinite(value)) return { error: 'Enter a number, like 0.5 or -1.' }
  if (value === 0) return { error: 'Use Exclude instead of 0.' }
  return { value }
}

export function FactorInput({ sourceKey, mapping: m, label }: { sourceKey: string; mapping: Mapping; label: string }) {
  const { mapping } = useEditor()
  const current = mappingMultiplier(m)
  const [draft, setDraft] = useState<string | null>(null)
  const errorId = useId()
  const text = draft ?? (current === 1 ? '' : String(current))
  const parsed = parseMultiplier(text)
  const invalid = draft !== null && !!parsed.error

  const commit = () => {
    if (draft === null || parsed.value === undefined) return
    if (parsed.value !== current) mapping.setMultiplier(sourceKey, m.id, parsed.value === 1 ? undefined : parsed.value)
    setDraft(null)
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Input
        inputMode="decimal"
        placeholder="1"
        value={text}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            if (e.nativeEvent.isComposing || e.keyCode === 229) return
            e.preventDefault()
            commit()
          } else if (e.key === 'Escape') {
            setDraft(null)
          }
        }}
        aria-label={`Factor for ${label}`}
        aria-invalid={invalid}
        aria-describedby={invalid ? errorId : undefined}
        className={cn(
          'h-7 w-20 text-right font-mono tabular-nums',
          current !== 1 && draft === null && 'border-primary/50 bg-primary/10 font-medium text-primary',
        )}
      />
      {invalid && (
        <p id={errorId} className="text-right text-xs text-destructive">
          {parsed.error}
        </p>
      )}
    </div>
  )
}
