import { Trash2Icon, XIcon } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { ReportSource, ReportSourceInput, ReportSourceVariable, VariableMissingBehavior, Violation } from '@/lib/reports/contract'
import { useNames, useReportEditor } from '../editor-state'
import { KeyText, Refusal, SectionLabel, WARN_CHIP } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'
import { ReportBridgeLine } from './report-bridge-line'

const INHERIT = '__inherit__'

/** One source: its name and time field, the variables it supplies, its coverage, and its delete, which takes its bridge (OI-56). */
export function SourceCard({ source }: { source: ReportSource }) {
  const { reportId, diagnostics, run, setUi } = useReportEditor()
  const names = useNames()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const coverage = diagnostics?.coverage[source.key]
  const edit = async (call: Parameters<typeof run>[0]) => setRefusal(await run(call))
  const identity: ReportSourceInput = {
    key: source.key,
    displayName: source.displayName,
    datasetId: source.datasetId,
    timeFieldKey: source.timeFieldKey,
    method: source.method,
    roleId: source.roleId,
    fieldKey: source.fieldKey,
  }

  return (
    <section className="flex flex-col gap-4 rounded-lg border bg-card p-4" aria-label={source.displayName}>
      <div className="flex flex-wrap items-start gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex items-center gap-2">
            <h3 className="font-medium">{source.displayName}</h3>
            <KeyText>{source.key}</KeyText>
            <Badge variant="outline">{source.method === 'bridge' ? 'Bridge' : 'Direct'}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">
            {names.dataset(source.datasetId)?.displayName ?? source.datasetId}
            {source.method === 'bridge' ? ` · role ${names.role(source.roleId)?.displayName ?? source.roleId}` : ` · field ${source.fieldKey}`}
          </p>
          {source.method === 'bridge' && <ReportBridgeLine sourceKey={source.key} />}
        </div>
        <div className="ml-auto flex items-center gap-2">
          {source.method === 'bridge' && coverage && (
            <Button size="sm" variant="outline" className={coverage.unmapped.length ? WARN_CHIP : undefined} onClick={() => setUi({ dialog: { type: 'coverage', sourceKey: source.key } })}>
              {coverage.unmapped.length ? `${coverage.unmapped.length} unmapped` : 'Coverage'}
            </Button>
          )}
          <Button size="icon-sm" variant="ghost" aria-label={`Delete ${source.displayName}`} onClick={() => void edit((api) => api.deleteSource(reportId, source.key))}>
            <Trash2Icon />
          </Button>
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1.5 text-sm">
          Name
          <Input
            defaultValue={source.displayName}
            onBlur={(e) => e.target.value !== source.displayName && void edit((api) => api.saveSource(reportId, { ...identity, displayName: e.target.value }))}
          />
        </label>
        <label className="flex flex-col gap-1.5 text-sm">
          Time field
          <Input
            className="font-mono"
            defaultValue={source.timeFieldKey}
            onBlur={(e) => e.target.value !== source.timeFieldKey && void edit((api) => api.saveSource(reportId, { ...identity, timeFieldKey: e.target.value.trim() }))}
          />
        </label>
      </div>
      <Refusal violations={refusal} />
      <SourceVariables source={source} />
    </section>
  )
}

/** The variables a source supplies, each from a dataset measure, with an optional override of the variable's missing behavior. */
function SourceVariables({ source }: { source: ReportSource }) {
  const { reportId, model, run } = useReportEditor()
  const [variables, setVariables] = useState<ReportSourceVariable[]>(source.variables)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const free = (model?.variables ?? []).filter((v) => !variables.some((s) => s.variableKey === v.key))
  const patch = (key: string, p: Partial<ReportSourceVariable>) => setVariables(variables.map((v) => (v.variableKey === key ? { ...v, ...p } : v)))

  return (
    <div className="flex flex-col gap-2">
      <SectionLabel>Variables supplied</SectionLabel>
      <ul className="flex flex-col gap-1.5">
        {variables.map((v) => (
          <li key={v.variableKey} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-40">{model?.variables.find((m) => m.key === v.variableKey)?.displayName ?? v.variableKey}</span>
            <span className="text-muted-foreground">from measure</span>
            <Input aria-label={`Measure of ${v.variableKey}`} className="h-7 w-40 font-mono" value={v.measureKey} onChange={(e) => patch(v.variableKey, { measureKey: e.target.value.trim() })} />
            <SimpleSelect
              aria-label={`Missing values of ${v.variableKey}`}
              size="sm"
              value={v.missingBehavior ?? INHERIT}
              onChange={(m) => patch(v.variableKey, { missingBehavior: m === INHERIT ? null : (m as VariableMissingBehavior) })}
              options={[
                { value: INHERIT, label: "Variable's missing behavior" },
                { value: 'null', label: 'Missing is null' },
                { value: 'zero', label: 'Missing is zero' },
              ]}
              className="w-56"
            />
            <Button size="icon-xs" variant="ghost" aria-label={`Remove ${v.variableKey}`} onClick={() => setVariables(variables.filter((x) => x !== v))}>
              <XIcon />
            </Button>
          </li>
        ))}
      </ul>
      <div className="flex items-center gap-2">
        {free.length > 0 && (
          <SimpleSelect
            aria-label="Variable to add"
            size="sm"
            value=""
            placeholder="Add a variable…"
            onChange={(variableKey) => setVariables([...variables, { variableKey, measureKey: '', filter: null, missingBehavior: null }])}
            options={free.map((v) => ({ value: v.key, label: v.displayName, hint: v.key }))}
            className="w-56"
          />
        )}
        <Button
          size="sm"
          className="ml-auto"
          disabled={JSON.stringify(variables) === JSON.stringify(source.variables) || variables.some((v) => !v.measureKey)}
          onClick={async () => setRefusal(await run((api) => api.setSourceVariables(reportId, source.key, variables)))}
        >
          Apply
        </Button>
      </div>
      <Refusal violations={refusal} />
    </div>
  )
}
