import { AlertTriangleIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useCatalog, useDatasetsApi, useVocabularies } from '@/lib/datasets/client'
import type { DatasetVersionSettings, DatasetViolation } from '@/lib/datasets/contract'
import { bindingLabel } from './bindings-section'
import type { SectionProps } from './dataset-detail'
import { CheckRow, FormRow, FormSection, NONE, ProblemBanner, SectionHeader, VocabSelect, useApiCall } from './shared'

export function SettingsSection({ version, readOnly, onOpen }: SectionProps) {
  const api = useDatasetsApi()
  const { data: vocab } = useVocabularies()
  const { data: catalog } = useCatalog()
  const { run, problem, setProblem, errorsFor } = useApiCall()
  const [d, setD] = useState<DatasetVersionSettings>(version.settings)
  const [synced, setSynced] = useState(version.settings)
  const [warnings, setWarnings] = useState<DatasetViolation[]>([])
  // Undo, redo and discard replace version.settings; adopt them unless an invalid edit is still being fixed.
  if (synced !== version.settings) {
    setSynced(version.settings)
    if (!problem) setD(version.settings)
  }
  const sourceKindLabel = vocab?.sourceObjectKind.find((t) => t.value === version.settings.sourceObjectKind)?.label ?? version.settings.sourceObjectKind
  const timeBindings = version.bindings.filter((b) => b.bindingKind === 'time')

  async function commit(edited: DatasetVersionSettings) {
    const next = { ...edited, sourceObjectKind: version.settings.sourceObjectKind, sourceObject: version.settings.sourceObject }
    setD(next)
    if (readOnly || JSON.stringify(next) === JSON.stringify(version.settings)) return
    const r = await run(() => api.saveSettings(version.id, next))
    if (!r) return
    setWarnings(r.warnings)
    if (r.warnings.length) toast.warning(`${r.warnings.length} column warning${r.warnings.length === 1 ? '' : 's'} in these settings`)
  }
  const set = <K extends keyof DatasetVersionSettings>(k: K, v: DatasetVersionSettings[K]) => commit({ ...d, [k]: v })
  const draft = <K extends keyof DatasetVersionSettings>(k: K, v: DatasetVersionSettings[K]) => setD((x) => ({ ...x, [k]: v }))
  const commitOnEnter = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && !e.nativeEvent.isComposing && e.keyCode !== 229) e.currentTarget.blur()
  }

  return (
    <div className="flex flex-col gap-4">
      <SectionHeader title="Settings" help="Version-level facts the planner needs before reading any field: where the data lives, which query modes it serves, its default time axis and its expression grammar." />
      {problem && <ProblemBanner problem={problem} onDismiss={() => setProblem(null)} />}
      {!!warnings.length && (
        <div role="status" className="flex gap-2 rounded-md border border-warning/50 bg-warning-muted p-3 text-sm">
          <AlertTriangleIcon aria-hidden className="size-4 shrink-0 text-warning-foreground" />
          <div className="flex flex-col gap-1">
            <p className="font-medium">Some fields point at columns the new source doesn&apos;t have</p>
            <ul className="flex flex-col gap-0.5 text-xs">
              {warnings.map((w) => (
                <li key={`${w.code}-${w.target.itemId}`}>
                  <button type="button" onClick={() => onOpen(w.target.area, w.target.itemId ?? null)} className="font-mono text-primary hover:underline">
                    {w.target.itemKey}
                  </button>{' '}
                  {w.message}
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
      <fieldset disabled={readOnly} className="flex max-w-4xl flex-col gap-4">
        <FormSection title="Source">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormRow label="Source kind" htmlFor="s-kind" hint="Set when the dataset was created. Only tables allow materializing columns.">
              <Input id="s-kind" readOnly value={sourceKindLabel} className="cursor-default bg-muted text-muted-foreground focus-visible:ring-0" />
            </FormRow>
            <FormRow label="Source object" htmlFor="s-obj" hint="Set when the dataset was created.">
              <Input id="s-obj" readOnly value={version.settings.sourceObject} className="cursor-default bg-muted font-mono text-muted-foreground focus-visible:ring-0" />
            </FormRow>
          </div>
        </FormSection>

        <FormSection title="Query modes" description="At least one mode must be on.">
          <div className="grid gap-3 sm:grid-cols-2">
            <CheckRow checked={d.supportsAggregate} onChange={(v) => set('supportsAggregate', v)} label="Aggregate queries" description="Grouped measures over bindings." disabled={readOnly} />
            <CheckRow checked={d.supportsDetail} onChange={(v) => set('supportsDetail', v)} label="Detail queries" description="Row-level projection of selectable fields." disabled={readOnly} />
          </div>
          {errorsFor('supportsAggregate')?.map((e) => (
            <p key={e} className="text-xs text-destructive">
              {e}
            </p>
          ))}
        </FormSection>

        <FormSection title="Defaults and grammar">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormRow label="Default time binding" htmlFor="s-dtb" errors={errorsFor('defaultTimeBindingId')} hint="The axis for queries whose measures require none.">
              <SimpleSelect
                id="s-dtb"
                value={d.defaultTimeBindingId ?? NONE}
                onChange={(v) => set('defaultTimeBindingId', v === NONE ? null : v)}
                options={[{ value: NONE, label: 'None' }, ...timeBindings.map((b) => ({ value: b.id, label: bindingLabel(b, version, catalog) }))]}
                className="font-mono"
              />
            </FormRow>
            <FormRow label="Expression language" htmlFor="s-elv" errors={errorsFor('expressionLanguageVersion')}>
              <VocabSelect id="s-elv" terms={vocab?.expressionLanguageVersion} value={d.expressionLanguageVersion} onChange={(v) => v && set('expressionLanguageVersion', v)} />
            </FormRow>
            <FormRow label="Security policy key" htmlFor="s-sec" errors={errorsFor('securityPolicyKey')} hint="Optional authorization policy identifier.">
              <Input id="s-sec" value={d.securityPolicyKey ?? ''} onChange={(e) => draft('securityPolicyKey', e.target.value || null)} onBlur={() => commit(d)} onKeyDown={commitOnEnter} className="font-mono" />
            </FormRow>
          </div>
        </FormSection>

        <FormSection title="Properties" description="Free JSON for extensions. Carries no query semantics.">
          <FormRow label="Properties JSON" htmlFor="s-props" errors={errorsFor('properties')}>
            <Textarea id="s-props" rows={5} value={d.properties} onChange={(e) => draft('properties', e.target.value)} onBlur={() => commit(d)} className="font-mono text-xs" spellCheck={false} />
          </FormRow>
        </FormSection>
      </fieldset>
      {!readOnly && (
        <p className="max-w-4xl text-xs text-muted-foreground">
          {problem ? 'Not applied — fix the highlighted inputs.' : 'Changes apply as you make them and join the batch saved from the header.'}
        </p>
      )}
    </div>
  )
}
