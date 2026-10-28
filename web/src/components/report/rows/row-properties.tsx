import { PlusIcon, XIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { uid, useEditor } from '@/lib/report/store'
import { useCalcModel } from '@/lib/report/use-calc-model'
import { findRow } from '@/lib/report/tree'
import type { RowKind, RowNode, RowScope } from '@/lib/report/types'
import { KIND_LABEL, SectionLabel } from '../shared/bits'
import { SearchCombobox } from '../shared/search-combobox'
import { Segmented } from '../shared/segmented'
import { SimpleSelect } from '../shared/simple-select'

const KINDS: RowKind[] = ['line', 'subtotal', 'heading', 'formula']
const ROW_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/

const KIND_HINT: Record<RowKind, string> = {
  line: 'Shows values mapped from sources.',
  subtotal: 'Totals the rows under it.',
  heading: 'Labels a group and shows no values.',
  formula: 'Shows a calculated measure.',
}

const SCOPE_OPTIONS = [
  { value: 'row', label: 'This row only' },
  { value: 'subtree', label: 'This row and rows under it' },
]

export function RowProperties({ row }: { row: RowNode }) {
  return <RowPropertiesForm key={row.key} row={row} />
}

function Field({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
  return (
    <section aria-labelledby={id} className="flex flex-col gap-3">
      <SectionLabel id={id}>{title}</SectionLabel>
      {children}
    </section>
  )
}

function RowPropertiesForm({ row }: { row: RowNode }) {
  const { report, ui, mutate } = useEditor()
  const calc = useCalcModel()
  const rowKey = row.key

  const updateRow = (fn: (r: RowNode) => void) =>
    mutate((d) => {
      const r = findRow(d.rows, rowKey)
      if (r) fn(r)
    })

  const mappingCount = report.sources.reduce((n, s) => n + s.mappings.filter((m) => m.rowKey === rowKey).length, 0)
  const measureError = row.kind === 'formula' && !row.measureKey ? 'Pick a measure' : null

  const changeKind = (kind: RowKind) => {
    if (kind === row.kind) return
    const drops = row.kind === 'line' && mappingCount > 0
    mutate((d) => {
      const r = findRow(d.rows, rowKey)
      if (!r) return
      r.kind = kind
      if (kind !== 'formula') {
        delete r.measureKey
        delete r.columnKey
      }
      if (drops) for (const s of d.sources) s.mappings = s.mappings.filter((m) => m.rowKey !== rowKey)
    })
    if (drops) {
      toast(`Removed ${mappingCount} ${mappingCount === 1 ? 'mapping' : 'mappings'} from "${row.label}"`, {
        description: 'Only lines take mappings. Discard in the top bar to undo.',
      })
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Field id="rp-kind" title="Row type">
        <Segmented
          aria-label="Row type"
          value={row.kind}
          onChange={changeKind}
          options={KINDS.map((k) => ({ value: k, label: KIND_LABEL[k] }))}
        />
        <p className="text-xs text-muted-foreground">
          {row.kind === 'line' && mappingCount > 0
            ? `${KIND_HINT.line} Changing the type removes its ${mappingCount} ${mappingCount === 1 ? 'mapping' : 'mappings'}.`
            : KIND_HINT[row.kind]}
        </p>

        {row.kind === 'formula' && (
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="rp-measure">Calculated measure</Label>
              <SearchCombobox
                id="rp-measure"
                value={row.measureKey ?? ''}
                onChange={(v) =>
                  updateRow((r) => {
                    r.measureKey = v
                  })
                }
                options={calc.measures.map((m) => ({ value: m.key, label: m.name, hint: m.key }))}
                placeholder="Choose a measure"
              />
              {measureError && <p className="text-xs text-destructive">{measureError}</p>}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="rp-column">Show in column</Label>
              <SearchCombobox
                id="rp-column"
                value={row.columnKey ?? ''}
                onChange={(v) =>
                  updateRow((r) => {
                    r.columnKey = v || undefined
                  })
                }
                options={report.columns.map((c) => ({ value: c.key, label: c.label, hint: c.key }))}
                placeholder="First visible column"
              />
            </div>
          </div>
        )}
      </Field>

      <Field id="rp-display" title="Display">
        <label className="flex items-center justify-between gap-4 rounded-lg border px-3 py-2.5">
          <span className="flex flex-col gap-0.5">
            <span className="font-medium">Hide this row</span>
            <span className="text-xs text-muted-foreground">Still counts toward subtotals, just not shown.</span>
          </span>
          <Switch
            checked={row.hidden ?? false}
            onCheckedChange={(hidden) =>
              updateRow((r) => {
                r.hidden = hidden
              })
            }
          />
        </label>
      </Field>

      {ui.advanced && (
        <Field id="rp-advanced" title="Advanced">
          <RowKeyField row={row} />
          <RowScopesField row={row} />
        </Field>
      )}
    </div>
  )
}

function RowKeyField({ row }: { row: RowNode }) {
  const { report, ui, mutate, setUi } = useEditor()
  const rowKey = row.key
  const [key, setKey] = useState(rowKey)
  const nextKey = key.trim()
  const renames = nextKey !== rowKey
  const keyError = !nextKey
    ? 'Enter a key'
    : !ROW_KEY_PATTERN.test(nextKey)
      ? 'Use letters, digits and underscores, starting with a letter'
      : renames && findRow(report.rows, nextKey)
        ? `Another row already uses ${nextKey}`
        : null

  const commit = () => {
    if (!renames) return
    if (keyError) {
      setKey(rowKey)
      return
    }
    mutate((d) => {
      const r = findRow(d.rows, rowKey)
      if (!r) return
      r.key = nextKey
      for (const s of d.sources) for (const m of s.mappings) if (m.rowKey === rowKey) m.rowKey = nextKey
      for (const s of d.rowScopes) if (s.rowKey === rowKey) s.rowKey = nextKey
    })
    setUi({
      selectedRowKey: ui.selectedRowKey === rowKey ? nextKey : ui.selectedRowKey,
      collapsed: ui.collapsed.map((k) => (k === rowKey ? nextKey : k)),
    })
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="rp-key">Row key</Label>
      <Input
        id="rp-key"
        value={key}
        onChange={(e) => setKey(e.target.value.replace(/\s+/g, '_'))}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.nativeEvent.isComposing || e.keyCode === 229) return
          if (e.key === 'Enter') {
            e.preventDefault()
            commit()
          } else if (e.key === 'Escape') {
            e.preventDefault()
            setKey(rowKey)
          }
        }}
        aria-invalid={renames && !!keyError}
        aria-describedby="rp-key-hint"
        className="font-mono"
        spellCheck={false}
        autoComplete="off"
      />
      <p id="rp-key-hint" className={renames && keyError ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'}>
        {renames && keyError
          ? keyError
          : renames
            ? `Mappings and scopes on ${rowKey} move to ${nextKey}.`
            : 'Used by sources that match on row key and in exports.'}
      </p>
    </div>
  )
}

function RowScopesField({ row }: { row: RowNode }) {
  const { report, mutate } = useEditor()
  const calc = useCalcModel()
  const rowKey = row.key
  const scopes = report.rowScopes.filter((s) => s.rowKey === rowKey)
  const [drafts, setDrafts] = useState<RowScope[]>([])
  const variableOptions = calc.variables.map((v) => ({ value: v.key, label: v.name, hint: v.key }))

  const updateScope = (id: string, patch: Partial<RowScope>) =>
    mutate((d) => {
      const s = d.rowScopes.find((x) => x.id === id)
      if (s) Object.assign(s, patch)
    })

  const removeScope = (id: string) =>
    mutate((d) => {
      d.rowScopes = d.rowScopes.filter((x) => x.id !== id)
    })

  const commitDraft = (draft: RowScope, variable: string) => {
    if (!variable) return
    mutate((d) => {
      d.rowScopes.push({ ...draft, variable })
    })
    setDrafts((all) => all.filter((x) => x.id !== draft.id))
  }

  const rows = [...scopes.map((s) => ({ scope: s, draft: false })), ...drafts.map((s) => ({ scope: s, draft: true }))]

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">Variable scopes</span>
        <Button
          type="button"
          size="xs"
          variant="ghost"
          onClick={() => setDrafts((d) => [...d, { id: uid('scope'), variable: '', rowKey, scope: 'row' }])}
        >
          <PlusIcon /> Add scope
        </Button>
      </div>
      {rows.length === 0 ? (
        <p className="text-xs text-muted-foreground">No variables are scoped to this row.</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {rows.map(({ scope: s, draft }) => (
            <li key={s.id} className="flex items-center gap-2">
              <SimpleSelect
                size="sm"
                className="flex-1 font-mono"
                aria-label="Variable"
                placeholder="Variable"
                value={s.variable}
                options={variableOptions}
                onChange={(variable) => (draft ? commitDraft(s, variable) : updateScope(s.id, { variable }))}
              />
              <SimpleSelect
                size="sm"
                className="flex-1"
                aria-label="Applies to"
                value={s.scope}
                options={SCOPE_OPTIONS}
                onChange={(v) => {
                  const scope = v as RowScope['scope']
                  if (draft) setDrafts((all) => all.map((x) => (x.id === s.id ? { ...x, scope } : x)))
                  else updateScope(s.id, { scope })
                }}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label="Remove scope"
                onClick={() => (draft ? setDrafts((all) => all.filter((x) => x.id !== s.id)) : removeScope(s.id))}
              >
                <XIcon />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
