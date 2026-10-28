import { useState } from 'react'
import { Button } from '@/components/ui/button'
import type { ReportSelectionScope, ReportVariableScope, Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { Refusal } from '../shared/bits'
import { Segmented } from '../shared/segmented'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none__'

/** One scope per variable (OI-50): the member row whose branch it reads, the row alone (`exact`, a leaf) or with what is under it. */
export function ScopesSection() {
  const { reportId, report, model, run } = useReportEditor()
  const [scopes, setScopes] = useState<ReportVariableScope[]>(report.variableScopes)
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const memberRows = report.rows.filter((r) => r.kind === 'data' || r.kind === 'group')
  const scopeOf = (variableKey: string) => scopes.find((s) => s.variableKey === variableKey)
  const set = (variableKey: string, scope: ReportVariableScope | null) =>
    setScopes([...scopes.filter((s) => s.variableKey !== variableKey), ...(scope ? [scope] : [])])

  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col divide-y rounded-lg border bg-card">
        {(model?.variables ?? []).map((v) => {
          const scope = scopeOf(v.key)
          return (
            <li key={v.key} className="flex flex-wrap items-center gap-3 px-3 py-2 text-sm">
              <span className="min-w-48">{v.displayName}</span>
              <SimpleSelect
                aria-label={`Scope of ${v.key}`}
                size="sm"
                value={scope?.scopeMemberKey ?? NONE}
                onChange={(row) => set(v.key, row === NONE ? null : { variableKey: v.key, scopeMemberKey: row, selectionScope: scope?.selectionScope ?? 'descendants' })}
                options={[{ value: NONE, label: 'Every row' }, ...memberRows.map((r) => ({ value: r.key, label: r.label, hint: r.key }))]}
                className="w-56"
              />
              {scope && (
                <Segmented<ReportSelectionScope>
                  aria-label={`Selection of ${v.key}`}
                  value={scope.selectionScope}
                  onChange={(selectionScope) => set(v.key, { ...scope, selectionScope })}
                  options={[
                    { value: 'descendants', label: 'Row and below' },
                    { value: 'exact', label: 'Row only' },
                  ]}
                />
              )}
            </li>
          )
        })}
      </ul>
      <Refusal violations={refusal} />
      <div>
        <Button
          disabled={JSON.stringify([...scopes].sort((a, b) => a.variableKey.localeCompare(b.variableKey))) === JSON.stringify(report.variableScopes)}
          onClick={async () => setRefusal(await run((api) => api.setVariableScopes(reportId, scopes)))}
        >
          Apply
        </Button>
      </div>
    </div>
  )
}
