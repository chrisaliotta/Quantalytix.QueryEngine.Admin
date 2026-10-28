import { DatabaseIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { SOURCE_DIMENSION_ROLES } from '@/lib/report/dimensions'
import { uid, useEditor } from '@/lib/report/store'
import type { Dataset, Source, SourceMethod } from '@/lib/report/types'
import { SearchCombobox } from '../shared/search-combobox'
import { Segmented } from '../shared/segmented'

const KEY_PATTERN = /^[a-z][a-z0-9_]*$/

type FieldKey = 'name' | 'key' | 'dataset' | 'method' | 'dimension' | 'rowKeyField' | 'timeField'
type FieldValues = Record<FieldKey, string>
type FieldErrors = Partial<Record<FieldKey, string | null>>

function keyError(key: string, sources: Source[], ownKey?: string) {
  const k = key.trim()
  if (!k) return 'Enter a key'
  if (!KEY_PATTERN.test(k)) return 'Lowercase letters, digits and _ only'
  if (k !== ownKey && sources.some((s) => s.key === k)) return 'Another source uses this key'
  return null
}

interface SourceFieldsProps {
  idPrefix: string
  values: FieldValues
  errors: FieldErrors
  datasets: Dataset[]
  onChange: (field: FieldKey, value: string) => void
  onKeyBlur?: () => void
}

function SourceFields({ idPrefix, values, errors, datasets, onChange, onKeyBlur }: SourceFieldsProps) {
  const ds = datasets.find((d) => d.key === values.dataset)
  const id = (f: FieldKey) => `${idPrefix}-${f}`
  const err = (f: FieldKey) =>
    errors[f] ? (
      <p id={`${id(f)}-err`} className="text-xs text-destructive">
        {errors[f]}
      </p>
    ) : null
  const invalid = (f: FieldKey) => ({ 'aria-invalid': !!errors[f], 'aria-describedby': errors[f] ? `${id(f)}-err` : undefined })

  return (
    <div className="grid gap-x-6 gap-y-4 md:grid-cols-2 xl:grid-cols-4">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id('name')}>Name</Label>
        <Input id={id('name')} value={values.name} onChange={(e) => onChange('name', e.target.value)} placeholder="GL quarterly balances" {...invalid('name')} />
        {err('name')}
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id('key')}>Key</Label>
        <Input
          id={id('key')}
          value={values.key}
          onChange={(e) => onChange('key', e.target.value)}
          onBlur={onKeyBlur}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && onKeyBlur) {
              e.preventDefault()
              onKeyBlur()
            }
          }}
          className="font-mono"
          placeholder="gl_quarterly"
          {...invalid('key')}
        />
        {err('key')}
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id('dataset')}>Dataset</Label>
        <SearchCombobox
          id={id('dataset')}
          value={values.dataset}
          onChange={(v) => onChange('dataset', v)}
          options={datasets.map((d) => ({ value: d.key, label: d.name, hint: d.key }))}
          placeholder="Choose a dataset"
        />
        {err('dataset')}
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id('timeField')}>Time field</Label>
        <SearchCombobox
          id={id('timeField')}
          value={values.timeField}
          onChange={(v) => onChange('timeField', v)}
          options={(ds?.timeFields ?? []).map((f) => ({ value: f, label: f }))}
          placeholder={ds ? 'Choose a field' : 'Pick a dataset first'}
        />
        {err('timeField')}
      </div>
      <div className="flex flex-col gap-1.5 md:col-span-2">
        <span className="text-sm font-medium">How it reaches rows</span>
        <Segmented
          aria-label="How it reaches rows"
          value={values.method as SourceMethod}
          onChange={(v) => onChange('method', v)}
          options={[
            { value: 'mapped', label: 'Bridge from a dimension' },
            { value: 'matched', label: 'Direct on a field' },
          ]}
        />
      </div>
      {values.method === 'mapped' ? (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={id('dimension')}>Dimension role</Label>
          <SearchCombobox id={id('dimension')} value={values.dimension} onChange={(v) => onChange('dimension', v)} options={SOURCE_DIMENSION_ROLES} />
        </div>
      ) : (
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={id('rowKeyField')}>Row key field</Label>
          <SearchCombobox
            id={id('rowKeyField')}
            value={values.rowKeyField}
            onChange={(v) => onChange('rowKeyField', v)}
            options={(ds?.fields ?? []).map((f) => ({ value: f, label: f }))}
            placeholder={ds ? 'Choose a field' : 'Pick a dataset first'}
          />
          {err('rowKeyField')}
        </div>
      )}
    </div>
  )
}

export function NewSourceForm({ onCreated, onCancel }: { onCreated: (key: string) => void; onCancel: () => void }) {
  const { report, mutate } = useEditor()
  const [values, setValues] = useState<FieldValues>({
    name: '',
    key: '',
    dataset: '',
    method: 'mapped',
    dimension: 'account',
    rowKeyField: '',
    timeField: '',
  })
  const [startFrom, setStartFrom] = useState('')
  const [touched, setTouched] = useState(false)

  const errors: FieldErrors = {
    name: !values.name.trim() ? 'Enter a name' : null,
    key: keyError(values.key, report.sources),
    dataset: !values.dataset ? 'Pick a dataset' : null,
    rowKeyField: values.method === 'matched' && !values.rowKeyField ? 'Pick the field that holds row keys' : null,
    timeField: !values.timeField ? 'Pick a time field' : null,
  }
  const invalid = Object.values(errors).some(Boolean)
  const mappedSources = report.sources.filter((s) => s.method === 'mapped')

  const change = (field: FieldKey, value: string) =>
    setValues((v) => (field === 'dataset' ? { ...v, dataset: value, timeField: '', rowKeyField: '' } : { ...v, [field]: value }))

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    setTouched(true)
    if (invalid) return
    const key = values.key.trim()
    const method = values.method as SourceMethod
    const template = mappedSources.find((s) => s.key === startFrom)
    mutate((d) => {
      d.sources.push({
        key,
        name: values.name.trim(),
        dataset: values.dataset,
        method,
        dimension: method === 'mapped' ? values.dimension : undefined,
        rowKeyField: method === 'matched' ? values.rowKeyField : undefined,
        timeField: values.timeField,
        bindings: [],
        mappings: method === 'mapped' && template ? template.mappings.map((m) => ({ ...m, id: uid('m') })) : [],
        leftOut: method === 'mapped' && template ? [...template.leftOut] : [],
      })
    })
    toast(`Added source "${values.name.trim()}"`, { description: 'Supply its variables below.' })
    onCreated(key)
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col rounded-lg border bg-card" aria-labelledby="new-source-title">
      <header className="flex items-center gap-2 border-b px-4 py-3">
        <DatabaseIcon className="size-4 text-muted-foreground" aria-hidden />
        <div className="flex flex-col">
          <h3 id="new-source-title" className="font-semibold">
            New source
          </h3>
          <p className="text-muted-foreground">A dataset the report reads from, and how its records reach rows.</p>
        </div>
      </header>
      <div className="flex flex-col gap-4 px-4 py-4">
        <SourceFields idPrefix="ns" values={values} errors={touched ? errors : {}} datasets={report.datasets} onChange={change} />
        {values.method === 'mapped' && mappedSources.length > 0 && (
          <div className="flex flex-col gap-1.5 md:max-w-sm">
            <Label htmlFor="ns-start">
              Start from the mappings of <span className="font-normal text-muted-foreground">(optional)</span>
            </Label>
            <SearchCombobox
              id="ns-start"
              value={startFrom}
              onChange={(v) => setStartFrom(v === startFrom ? '' : v)}
              options={mappedSources.map((s) => ({ value: s.key, label: s.name, hint: `${s.mappings.length} mappings` }))}
              placeholder="Start empty"
            />
          </div>
        )}
      </div>
      <footer className="flex justify-end gap-2 border-t px-4 py-3">
        <Button type="button" variant="outline" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" size="sm">
          Add source
        </Button>
      </footer>
    </form>
  )
}

export function SourceProperties({ source, onRenamed }: { source: Source; onRenamed: (key: string) => void }) {
  const { report, mutate } = useEditor()
  const [keyDraft, setKeyDraft] = useState(source.key)
  const draftKeyError = keyError(keyDraft, report.sources, source.key)

  const values: FieldValues = {
    name: source.name,
    key: keyDraft,
    dataset: source.dataset,
    method: source.method,
    dimension: source.dimension ?? 'account',
    rowKeyField: source.rowKeyField ?? '',
    timeField: source.timeField,
  }
  const errors: FieldErrors = {
    name: !source.name.trim() ? 'Enter a name' : null,
    key: keyDraft !== source.key ? draftKeyError : null,
    timeField: !source.timeField ? 'Pick a time field' : null,
    rowKeyField: source.method === 'matched' && !source.rowKeyField ? 'Pick the field that holds row keys' : null,
  }

  const edit = (fn: (s: Source) => void) =>
    mutate((d) => {
      const s = d.sources.find((x) => x.key === source.key)
      if (s) fn(s)
    })

  const commitKey = () => {
    const next = keyDraft.trim()
    if (next === source.key || draftKeyError) return
    const prev = source.key
    mutate((d) => {
      const s = d.sources.find((x) => x.key === prev)
      if (s) s.key = next
      for (const p of d.profiles) {
        for (const variable of Object.keys(p.picks)) if (p.picks[variable] === prev) p.picks[variable] = next
      }
    })
    onRenamed(next)
  }

  const change = (field: FieldKey, value: string) => {
    if (field === 'key') return setKeyDraft(value)
    if (field === 'dataset') {
      if (value === source.dataset) return
      const ds = report.datasets.find((d) => d.key === value)
      const stale = source.bindings.filter((b) => !ds?.measures.includes(b.measure)).length
      edit((s) => {
        s.dataset = value
        if (!ds?.timeFields.includes(s.timeField)) s.timeField = ds?.timeFields[0] ?? ''
        if (s.rowKeyField && !ds?.fields.includes(s.rowKeyField)) s.rowKeyField = undefined
      })
      if (stale > 0) toast(`${stale} ${stale === 1 ? 'measure is' : 'measures are'} not in ${ds?.name ?? value}`, { description: 'Pick new measures for those variables.' })
      return
    }
    if (field === 'method') {
      const method = value as SourceMethod
      edit((s) => {
        s.method = method
        if (method === 'mapped') s.dimension = s.dimension ?? 'account'
      })
      return
    }
    edit((s) => {
      if (field === 'name') s.name = value
      else if (field === 'timeField') s.timeField = value
      else if (field === 'dimension') s.dimension = value
      else if (field === 'rowKeyField') s.rowKeyField = value
    })
  }

  return (
    <section className="flex flex-col rounded-lg border bg-card" aria-labelledby={`props-${source.key}`}>
      <header className="border-b px-4 py-3">
        <h4 id={`props-${source.key}`} className="font-semibold">
          Properties
        </h4>
      </header>
      <div className="px-4 py-4">
        <SourceFields idPrefix={`sp-${source.key}`} values={values} errors={errors} datasets={report.datasets} onChange={change} onKeyBlur={commitKey} />
      </div>
    </section>
  )
}
