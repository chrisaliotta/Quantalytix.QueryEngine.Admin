import { PlusIcon, XIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { Filter, FilterOp } from '@/lib/report/types'
import { SearchCombobox } from './search-combobox'
import { SimpleSelect } from './simple-select'

const LARGE_LIST = 25
const MAX_SHOWN = 50

const OPS: { value: FilterOp; label: string }[] = [
  { value: '=', label: 'is' },
  { value: '!=', label: 'is not' },
  { value: 'in', label: 'is one of' },
  { value: '>', label: 'greater than' },
  { value: '<', label: 'less than' },
]

export function opLabel(op: FilterOp) {
  return OPS.find((o) => o.value === op)?.label ?? op
}

export function filterSummary(f: Filter) {
  return `${f.field} ${f.op} ${f.value || '…'}`
}

interface FilterBuilderProps {
  filters: Filter[]
  onChange: (filters: Filter[]) => void
  /** `values` turns the value input into a picker for equality operators. */
  fields: { value: string; label: string; values?: { value: string; label: string }[] }[]
  addLabel?: string
}

export function FilterBuilder({ filters, onChange, fields, addLabel = 'Add condition' }: FilterBuilderProps) {
  const update = (i: number, patch: Partial<Filter>) => onChange(filters.map((f, j) => (j === i ? { ...f, ...patch } : f)))

  return (
    <div className="flex flex-col gap-2">
      {filters.length === 0 && <p className="text-muted-foreground">No conditions. Everything is included.</p>}
      {filters.length > 0 && (
        <ul className="flex flex-col gap-2" aria-label="Conditions">
          {filters.map((f, i) => (
            <li key={i} className="flex items-center gap-2">
              <span className="w-10 shrink-0 text-right text-xs text-muted-foreground">{i === 0 ? 'Where' : 'and'}</span>
              <SimpleSelect
                size="sm"
                className="w-36 font-mono"
                aria-label="Field"
                placeholder="Field"
                value={f.field}
                options={fields}
                onChange={(field) => update(i, { field })}
              />
              <SimpleSelect size="sm" className="w-32" aria-label="Operator" value={f.op} options={OPS} onChange={(op) => update(i, { op: op as FilterOp })} />
              {(() => {
                const values = fields.find((x) => x.value === f.field)?.values
                if (values && (f.op === '=' || f.op === '!=') && values.length > LARGE_LIST) {
                  return (
                    <div className="min-w-0 flex-1">
                      <SearchCombobox
                        aria-label="Value"
                        className="h-8"
                        placeholder="Value"
                        searchPlaceholder="Search members…"
                        value={f.value}
                        options={values}
                        limit={MAX_SHOWN}
                        onChange={(value) => update(i, { value })}
                      />
                    </div>
                  )
                }
                return values && (f.op === '=' || f.op === '!=') ? (
                  <SimpleSelect
                    size="sm"
                    className="min-w-0 flex-1"
                    aria-label="Value"
                    placeholder="Value"
                    value={f.value}
                    options={values}
                    onChange={(value) => update(i, { value })}
                  />
                ) : (
                  <Input
                    aria-label="Value"
                    value={f.value}
                    placeholder={f.op === 'in' ? 'a, b, c' : 'Value'}
                    onChange={(e) => update(i, { value: e.target.value })}
                    className="h-8 min-w-0 flex-1 font-mono"
                  />
                )
              })()}
              <Button type="button" variant="ghost" size="icon-sm" aria-label="Remove condition" onClick={() => onChange(filters.filter((_, j) => j !== i))}>
                <XIcon />
              </Button>
            </li>
          ))}
        </ul>
      )}
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="w-fit"
        onClick={() => onChange([...filters, { field: fields[0]?.value ?? '', op: '=', value: '' }])}
      >
        <PlusIcon /> {addLabel}
      </Button>
    </div>
  )
}
