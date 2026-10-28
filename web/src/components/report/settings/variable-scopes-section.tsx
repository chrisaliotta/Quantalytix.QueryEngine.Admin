import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { Button } from '@/components/ui/button'
import { uid, useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import type { RowNode, RowScope, ScopeKind } from '@/lib/report/types'
import { useCalcModel } from '@/lib/report/use-calc-model'
import { type ComboOption, SearchCombobox } from '../shared/search-combobox'
import { SimpleSelect } from '../shared/simple-select'
import { VariableScopeHelp } from './variable-scope-help'

const TH = 'h-9 px-3 text-left align-middle text-xs font-medium whitespace-nowrap text-muted-foreground'
const TD = 'px-2 py-1.5 align-top first:pl-3'
/** Pickers read as plain text until hovered, focused or open. */
const QUIET =
  'border-transparent bg-transparent shadow-none dark:bg-transparent hover:border-dashed hover:border-border hover:bg-muted/40 dark:hover:bg-muted/40 aria-expanded:border-input data-[popup-open]:border-input'

const SCOPE_OPTIONS: { value: ScopeKind; label: string }[] = [
  { value: 'row', label: 'This row only' },
  { value: 'subtree', label: 'This row and rows under it' },
]

const varFieldId = (id: string) => `vs-var-${id}`

/** Rows panel order with depth; formula rows are skipped, their children (if any) keep the parent's depth. */
function rowTreeOptions(rows: RowNode[], depth = 0, path: string[] = []): ComboOption[] {
  return rows.flatMap((r) =>
    r.kind === 'formula'
      ? rowTreeOptions(r.children, depth, path)
      : [
          { value: r.key, label: r.label, depth, path: path.join(' › ') },
          ...rowTreeOptions(r.children, depth + 1, [...path, r.label]),
        ],
  )
}

function rowProblem(scopes: RowScope[], s: RowScope, rowMissing: boolean) {
  if (!s.variable && !s.rowKey) return 'Choose a variable and a row.'
  if (!s.variable) return 'Choose a variable.'
  if (!s.rowKey) return 'Choose a row.'
  if (rowMissing) return 'This row no longer exists in the report.'
  if (scopes.some((x) => x.id !== s.id && x.variable === s.variable && x.rowKey === s.rowKey))
    return 'This variable already has a scope on this row.'
  return null
}

export function VariableScopesSection() {
  const { report, mutate, setReport } = useEditor()
  const undoAction = useUndoAction()
  const calc = useCalcModel()
  const [justAdded, setJustAdded] = useState<string | null>(null)

  useEffect(() => {
    if (!justAdded) return
    const trigger = document.getElementById(varFieldId(justAdded))
    trigger?.focus()
    trigger?.click()
    setJustAdded(null)
  }, [justAdded])

  const variableOptions = calc.variables.map((v) => ({ value: v.key, label: v.name, hint: v.key }))
  const rowOptions = rowTreeOptions(report.rows)

  const update = (id: string, patch: Partial<RowScope>) =>
    mutate((d) => {
      const s = d.rowScopes.find((x) => x.id === id)
      if (s) Object.assign(s, patch)
    })

  const add = () => {
    const id = uid()
    mutate((d) => {
      d.rowScopes.push({ id, variable: '', rowKey: '', scope: 'row' })
    })
    setJustAdded(id)
  }

  const remove = (scope: RowScope) => {
    mutate((d) => {
      d.rowScopes = d.rowScopes.filter((s) => s.id !== scope.id)
    })
    const name = calc.variables.find((v) => v.key === scope.variable)?.name
    toast(name ? `Removed scope for "${name}"` : 'Removed scope', { action: undoAction() })
  }

  const count = report.rowScopes.length

  return (
    <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_26rem]">
    <div className="flex min-w-0 flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground tabular-nums">
          {count} {count === 1 ? 'scope' : 'scopes'}
        </p>
        <Button variant="outline" size="sm" onClick={add}>
          <PlusIcon data-icon="inline-start" />
          Add scope
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <th scope="col" className={TH}>
                Variable
              </th>
              <th scope="col" className={TH}>
                Row
              </th>
              <th scope="col" className={TH}>
                Applies to
              </th>
              <th scope="col" className={TH}>
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {count === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-10 text-center text-muted-foreground">
                  No scopes. Each variable reads every row it is mapped to.
                </td>
              </tr>
            )}
            {report.rowScopes.map((s) => {
              const variableName = calc.variables.find((v) => v.key === s.variable)?.name ?? s.variable
              const rowMissing = !!s.rowKey && !findRow(report.rows, s.rowKey)
              const problem = rowProblem(report.rowScopes, s, rowMissing)
              const rows = rowMissing ? [...rowOptions, { value: s.rowKey, label: 'Missing row' }] : rowOptions
              const label = variableName || 'new scope'
              return (
                <tr key={s.id} className="border-b last:border-b-0 hover:bg-muted/30">
                  <td className={`${TD} w-80`}>
                    <SearchCombobox
                      id={varFieldId(s.id)}
                      className={QUIET}
                      value={s.variable}
                      onChange={(variable) => update(s.id, { variable })}
                      options={variableOptions}
                      placeholder="Choose a variable…"
                      searchPlaceholder="Search variables…"
                    />
                    {problem && <p className="px-3 pt-1 text-xs text-destructive">{problem}</p>}
                  </td>
                  <td className={`${TD} w-80`}>
                    <SearchCombobox
                      className={QUIET}
                      value={s.rowKey}
                      onChange={(rowKey) => update(s.id, { rowKey })}
                      options={rows}
                      placeholder="Choose a row…"
                      searchPlaceholder="Search rows…"
                    />
                  </td>
                  <td className={TD}>
                    <SimpleSelect
                      size="sm"
                      aria-label={`Applies to for ${label}`}
                      className={`${QUIET} max-w-72`}
                      value={s.scope}
                      options={SCOPE_OPTIONS}
                      onChange={(scope) => update(s.id, { scope: scope as ScopeKind })}
                    />
                  </td>
                  <td className={`${TD} w-12 text-right`}>
                    <Button variant="ghost" size="icon-sm" aria-label={`Delete scope for ${label}`} onClick={() => remove(s)}>
                      <Trash2Icon />
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
    <VariableScopeHelp />
    </div>
  )
}
