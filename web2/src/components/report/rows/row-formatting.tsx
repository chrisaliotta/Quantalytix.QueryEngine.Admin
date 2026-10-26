import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { ModelParameter } from '@/lib/model/types'
import { useModels } from '@/lib/model/store'
import { resolveParameter, resolveStyle, roleOf, styleCss } from '@/lib/report/rowStyle'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import type { RowNode } from '@/lib/report/types'
import { SimpleSelect } from '../shared/simple-select'

const INHERIT = '__inherit'

function sourceText(source: string | null) {
  return source ? `inherited from ${source}` : "the model's default"
}

function useRowEditor(rowKey: string) {
  const { mutate } = useEditor()
  return (fn: (r: RowNode) => void) =>
    mutate((d) => {
      const r = findRow(d.rows, rowKey)
      if (r) fn(r)
    })
}

export function RowStyle({ row }: { row: RowNode }) {
  const { report } = useEditor()
  const editRow = useRowEditor(row.key)
  const role = roleOf(report.rows, row)
  const resolved = resolveStyle(report.styles, report.rows, row)

  const styleOptions = [
    { value: INHERIT, label: `Its role's default style` },
    ...report.styles.map((s) => ({ value: s.key, label: `${s.name}${s.defaultRole ? ` · ${s.defaultRole} default` : ''}` })),
  ]

  return (
    <section aria-label="Style" className="flex flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="row-style">Style</Label>
        <SimpleSelect id="row-style" value={row.styleKey || INHERIT} onChange={(v) => editRow((r) => void (r.styleKey = v === INHERIT ? undefined : v))} options={styleOptions} />
        <div className="flex items-center justify-between gap-3 rounded-md border bg-background px-3 py-1.5 text-sm" style={styleCss(resolved.style)}>
          <span className="truncate">{row.label}</span>
          <span className="font-mono tabular-nums">12,345.67</span>
        </div>
        <p className="text-xs text-muted-foreground">
          This row&apos;s role is <span className="font-medium text-foreground">{role}</span>.{' '}
          {resolved.inherited
            ? resolved.style
              ? `It uses ${resolved.style.name}, the ${role} default.`
              : `No style is the ${role} default, so it keeps the viewer's look.`
            : `It overrides the ${role} default.`}
        </p>
      </div>
    </section>
  )
}

export function RowParameters({ row }: { row: RowNode }) {
  const { report } = useEditor()
  const { getModel } = useModels()
  const editRow = useRowEditor(row.key)
  const model = getModel(report.settings.model)
  const params = model?.language === '1.2' ? model.parameters : []

  const setParam = (key: string, value: string | number | null) =>
    editRow((r) => {
      const next = { ...(r.parameters ?? {}) }
      if (value === null || value === '') delete next[key]
      else next[key] = value
      r.parameters = next
    })

  return (
    <section aria-label="Parameters" className="flex flex-col gap-4">
      {params.map((p) => (
        <ParameterField key={p.key} param={p} row={row} rows={report.rows} onChange={(v) => setParam(p.key, v)} />
      ))}
      {model && model.language !== '1.2' ? (
        <p className="text-xs text-pretty text-muted-foreground">Rows can set parameters when the report&apos;s calculation model uses formula language 1.2.</p>
      ) : (
        params.length === 0 && <p className="text-xs text-muted-foreground">The calculation model declares no parameters.</p>
      )}
    </section>
  )
}

function ParameterField({ param, row, rows, onChange }: { param: ModelParameter; row: RowNode; rows: RowNode[]; onChange: (v: string | number | null) => void }) {
  const own = row.parameters?.[param.key]
  const hasOwn = own !== undefined && own !== ''
  const inherited = resolveParameter(rows, row.key, param, true)
  const id = `row-param-${param.key}`
  const inheritLabel = `Inherit · ${inherited.value ?? 'no value'} (${sourceText(inherited.source)})`

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={id}>{param.name}</Label>
        {hasOwn && (
          <Button variant="ghost" size="sm" className="h-auto px-1.5 py-0.5 text-xs" onClick={() => onChange(null)}>
            Clear to inherit
          </Button>
        )}
      </div>
      {param.type === 'text' && param.allowedValues.length > 0 ? (
        <SimpleSelect
          id={id}
          value={hasOwn ? String(own) : INHERIT}
          onChange={(v) => onChange(v === INHERIT ? null : v)}
          options={[{ value: INHERIT, label: inheritLabel }, ...param.allowedValues.map((v) => ({ value: v, label: v }))]}
        />
      ) : (
        <Input
          id={id}
          inputMode={param.type === 'number' ? 'decimal' : undefined}
          value={hasOwn ? String(own) : ''}
          placeholder={inheritLabel}
          className={param.type === 'number' ? 'font-mono tabular-nums' : undefined}
          onChange={(e) => {
            const v = e.target.value
            onChange(param.type === 'number' && v !== '' && !Number.isNaN(Number(v)) ? Number(v) : v)
          }}
        />
      )}
      <p className="text-xs text-muted-foreground">
        {hasOwn ? `Set on this row; rows under it inherit ${own}.` : `Uses ${inherited.value ?? 'no value'}, ${sourceText(inherited.source)}.`}
        {row.kind === 'formula' && ' Formula rows inherit from the total row only.'}
      </p>
    </div>
  )
}
