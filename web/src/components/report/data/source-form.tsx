import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { KEY_PATTERN } from '@/lib/api/contract'
import type { ReportSourceInput, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { Refusal } from '../shared/bits'
import { Segmented } from '../shared/segmented'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none__'

/** A new source: its key, dataset and method are fixed once saved; it may start as a copy of another source's content. */
export function SourceForm({ onDone }: { onDone: () => void }) {
  const { reportId, report, catalog, run } = useReportEditor()
  const [input, setInput] = useState<ReportSourceInput>({
    key: '',
    displayName: '',
    datasetId: '',
    timeFieldKey: '',
    method: 'bridge',
    roleId: null,
    fieldKey: null,
  })
  const [copyFrom, setCopyFrom] = useState(NONE)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const patch = (p: Partial<ReportSourceInput>) => setInput((i) => ({ ...i, ...p }))
  const keyError = input.key && !KEY_PATTERN.test(input.key) ? 'Start with a letter; use letters, digits and _.' : null
  const complete = !!input.key && !keyError && !!input.displayName.trim() && !!input.datasetId && !!input.timeFieldKey && (input.method === 'bridge' ? !!input.roleId : !!input.fieldKey)

  const save = async () => {
    const violations = await run((api) => api.saveSource(reportId, { ...input, copyFromSourceKey: copyFrom === NONE ? undefined : copyFrom }))
    setRefusal(violations)
    if (!violations) onDone()
  }

  return (
    <form
      className="flex flex-col gap-4 rounded-lg border bg-card p-4"
      onSubmit={(e) => {
        e.preventDefault()
        void save()
      }}
    >
      <h3 className="font-medium">New source</h3>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-2">
          <Label htmlFor="source-name">Name</Label>
          <Input id="source-name" value={input.displayName} onChange={(e) => patch({ displayName: e.target.value })} autoFocus />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="source-key">Key</Label>
          <Input id="source-key" className="font-mono" value={input.key} onChange={(e) => patch({ key: e.target.value.trim() })} aria-invalid={!!keyError} />
          {keyError && <p className="text-xs text-destructive">{keyError}</p>}
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="source-dataset">Dataset</Label>
          <SimpleSelect
            id="source-dataset"
            value={input.datasetId}
            onChange={(datasetId) => patch({ datasetId })}
            placeholder="Choose a dataset"
            options={(catalog?.datasets ?? []).map((d) => ({ value: d.id, label: d.displayName, hint: d.key }))}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="source-time">Time field</Label>
          <Input id="source-time" className="font-mono" value={input.timeFieldKey} onChange={(e) => patch({ timeFieldKey: e.target.value.trim() })} placeholder="as_of_date" />
        </div>
        <div className="flex flex-col gap-2">
          <Label>Rows reached by</Label>
          <Segmented
            aria-label="Mapping method"
            value={input.method}
            onChange={(method) => patch({ method, roleId: null, fieldKey: null })}
            options={[
              { value: 'bridge', label: 'Bridge from a role' },
              { value: 'direct', label: 'Field of row keys' },
            ]}
          />
        </div>
        {input.method === 'bridge' ? (
          <div className="flex flex-col gap-2">
            <Label htmlFor="source-role">Role</Label>
            <SimpleSelect
              id="source-role"
              value={input.roleId ?? ''}
              onChange={(roleId) => patch({ roleId })}
              placeholder="Choose a role"
              options={(catalog?.roles ?? []).map((r) => ({ value: r.id, label: r.displayName, hint: r.key }))}
            />
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            <Label htmlFor="source-field">Field</Label>
            <Input id="source-field" className="font-mono" value={input.fieldKey ?? ''} onChange={(e) => patch({ fieldKey: e.target.value.trim() || null })} />
          </div>
        )}
        <div className="flex flex-col gap-2">
          <Label htmlFor="source-copy">Start as a copy of</Label>
          <SimpleSelect
            id="source-copy"
            value={copyFrom}
            onChange={setCopyFrom}
            options={[{ value: NONE, label: 'Nothing' }, ...report.sources.filter((s) => s.method === input.method).map((s) => ({ value: s.key, label: s.displayName, hint: s.key }))]}
          />
        </div>
      </div>
      <Refusal violations={refusal} />
      <div className="flex gap-2">
        <Button type="submit" disabled={!complete}>
          Add source
        </Button>
        <Button type="button" variant="outline" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
