import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import type { ReportRow, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { KIND_LABEL, KeyText, Refusal, SectionLabel } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'
import { RowResolution } from './row-resolution'
import { RowRules } from './row-rules'
import { indexOf, isMemberKind } from './row-model'

const NONE = '__none__'

/** The selected row: its properties and parameter values, the bridge rules into it, and what it shows under a profile. */
export function RowPanel() {
  const { report, ui } = useReportEditor()
  const row = report.rows.find((r) => r.key === ui.selectedRowKey) ?? report.rows[0]
  const bridgeSources = report.sources.filter((s) => s.method === 'bridge')
  return (
    <div className="flex flex-col gap-6 p-4 md:p-6">
      <RowProperties key={row.key} row={row} />
      {row.kind === 'data' && (
        <section className="flex flex-col gap-3">
          <SectionLabel>Items mapped to this row</SectionLabel>
          {bridgeSources.length === 0 && <p className="text-sm text-muted-foreground">No bridge source; add one on the Data tab.</p>}
          {bridgeSources.map((source) => (
            <RowRules key={`${source.key}:${row.key}`} source={source} rowKey={row.key} />
          ))}
        </section>
      )}
      {row.kind === 'data' && <RowResolution rowKey={row.key} />}
    </div>
  )
}

/** A row's label, kind, visibility, formula references, style and own parameter values, applied as one edit. */
function RowProperties({ row }: { row: ReportRow }) {
  const { reportId, report, model, run } = useReportEditor()
  const [draft, setDraft] = useState(row)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const patch = (p: Partial<ReportRow>) => setDraft((d) => ({ ...d, ...p }))
  const kinds = isMemberKind(row.kind) ? (['data', 'group'] as const) : (['section', 'formula'] as const)

  const apply = async () => {
    const next = { ...draft, showsValues: draft.kind === 'group' ? draft.showsValues : true }
    setRefusal(await run((api) => api.saveRow(reportId, { ...next, index: indexOf(report, row) })))
  }
  const setParameter = (key: string, raw: string, numeric: boolean) => {
    const { [key]: _, ...rest } = draft.parameters
    patch({ parameters: raw === '' ? rest : { ...rest, [key]: numeric ? Number(raw) : raw } })
  }

  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-baseline gap-2">
        <h2 className="text-base font-semibold">{row.label}</h2>
        <KeyText>{row.key}</KeyText>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-2">
          <Label htmlFor="row-label">Label</Label>
          <Input id="row-label" value={draft.label} onChange={(e) => patch({ label: e.target.value })} />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="row-kind">Kind</Label>
          <SimpleSelect
            id="row-kind"
            value={draft.kind}
            onChange={(kind) => patch({ kind: kind as ReportRow['kind'] })}
            options={kinds.map((k) => ({ value: k, label: KIND_LABEL[k] }))}
          />
        </div>
        {draft.kind === 'formula' && (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="row-measure">Calculated measure</Label>
              <SimpleSelect
                id="row-measure"
                value={draft.calculatedMeasureKey ?? ''}
                onChange={(calculatedMeasureKey) => patch({ calculatedMeasureKey })}
                options={(model?.measures ?? []).map((m) => ({ value: m.key, label: m.displayName, hint: m.key }))}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="row-column">Column</Label>
              <SimpleSelect
                id="row-column"
                value={draft.valueColumnKey ?? ''}
                onChange={(valueColumnKey) => patch({ valueColumnKey })}
                options={report.columns.map((c) => ({ value: c.key, label: c.displayName ?? c.metricKey, hint: c.key }))}
              />
            </div>
          </>
        )}
        <div className="flex flex-col gap-2">
          <Label htmlFor="row-style">Style</Label>
          <SimpleSelect
            id="row-style"
            value={draft.styleKey ?? NONE}
            onChange={(v) => patch({ styleKey: v === NONE ? null : v })}
            options={[{ value: NONE, label: 'Role default' }, ...report.styles.map((s) => ({ value: s.key, label: s.displayName, hint: s.key }))]}
          />
        </div>
        <div className="flex flex-col gap-3 pt-6">
          <label className="flex items-center gap-2 text-sm">
            <Switch checked={draft.isVisible} onCheckedChange={(isVisible) => patch({ isVisible })} /> Visible
          </label>
          {draft.kind === 'group' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={draft.showsValues} onCheckedChange={(showsValues) => patch({ showsValues })} /> Shows its total
            </label>
          )}
        </div>
      </div>
      {(model?.parameters.length ?? 0) > 0 && (
        <div className="flex flex-col gap-2">
          <SectionLabel>Parameter values</SectionLabel>
          <p className="text-xs text-muted-foreground">A parameter left empty takes its nearest ancestor's value, else the model's default.</p>
          <div className="grid gap-3 sm:grid-cols-2">
            {model!.parameters.map((p) => (
              <div key={p.key} className="flex flex-col gap-1.5">
                <Label htmlFor={`param-${p.key}`}>{p.displayName}</Label>
                {p.allowedValues ? (
                  <SimpleSelect
                    id={`param-${p.key}`}
                    value={String(draft.parameters[p.key] ?? NONE)}
                    onChange={(v) => setParameter(p.key, v === NONE ? '' : v, p.valueType === 'number')}
                    options={[{ value: NONE, label: 'Inherit' }, ...p.allowedValues.map((v) => ({ value: v, label: v }))]}
                  />
                ) : (
                  <Input
                    id={`param-${p.key}`}
                    type={p.valueType === 'number' ? 'number' : 'text'}
                    value={String(draft.parameters[p.key] ?? '')}
                    placeholder="Inherit"
                    onChange={(e) => setParameter(p.key, e.target.value, p.valueType === 'number')}
                  />
                )}
              </div>
            ))}
          </div>
        </div>
      )}
      <Refusal violations={refusal} />
      <div>
        <Button size="sm" onClick={() => void apply()} disabled={JSON.stringify(draft) === JSON.stringify(row)}>
          Apply
        </Button>
      </div>
    </section>
  )
}
