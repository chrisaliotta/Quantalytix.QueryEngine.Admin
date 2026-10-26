import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { cn } from '@/lib/utils'

export interface Option {
  value: string
  label: string
  hint?: string
}

interface SimpleSelectProps {
  value: string
  onChange: (value: string) => void
  options: Option[]
  placeholder?: string
  size?: 'sm' | 'default'
  className?: string
  id?: string
  disabled?: boolean
  'aria-label'?: string
}

export function SimpleSelect({ value, onChange, options, placeholder, size = 'default', className, id, disabled, ...rest }: SimpleSelectProps) {
  return (
    <Select
      value={value || null}
      onValueChange={(v) => v != null && onChange(String(v))}
      items={options.map((o) => ({ value: o.value, label: o.label }))}
      disabled={disabled}
    >
      <SelectTrigger id={id} size={size} className={cn('w-full min-w-0', className)} aria-label={rest['aria-label']}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((o) => (
          <SelectItem key={o.value} value={o.value}>
            <span className="truncate">{o.label}</span>
            {o.hint && <span className="ml-auto font-mono text-xs text-muted-foreground">{o.hint}</span>}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
