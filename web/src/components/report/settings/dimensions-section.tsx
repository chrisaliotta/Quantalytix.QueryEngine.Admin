import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { KEY_PATTERN } from '@/lib/api/contract'
import type { Grouping, ReportCalculationDimension, Violation } from '@/lib/reports/contract'
import { usePublishedMembers } from '@/lib/tenant/client'
import { useNames, useReportEditor } from '../editor-state'
import { KeyText, Refusal } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

/**
 * The calculation dimensions: roles a run breaks out or filters by, each grouped by member, level, ancestor or depth. Deleting one
 * also removes it from the default breakout and filter (OI-56).
 */
export function DimensionsSection() {
  const { reportId, report, catalog, run } = useReportEditor()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const [newKey, setNewKey] = useState('')
  const [newRole, setNewRole] = useState('')
  const edit = async (call: Parameters<typeof run>[0]) => setRefusal(await run(call))

  const add = async () => {
    const role = catalog!.roles.find((r) => r.id === newRole)!
    await edit((api) =>
      api.saveCalculationDimension(reportId, { key: newKey, displayName: role.displayName, roleId: role.id, grouping: { mode: 'member' }, isGroupable: true, isFilterable: true, isEnabled: true }),
    )
    setNewKey('')
  }

  return (
    <div className="flex flex-col gap-4">
      {report.calculationDimensions.map((d) => (
        <DimensionRow key={d.key} dimension={d} onEdit={edit} />
      ))}
      {report.calculationDimensions.length === 0 && <p className="text-sm text-muted-foreground">No calculation dimensions yet.</p>}
      <Refusal violations={refusal} />
      <form
        className="flex flex-wrap items-center gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          void add()
        }}
      >
        <Input aria-label="New dimension key" className="w-40 font-mono" placeholder="region" value={newKey} onChange={(e) => setNewKey(e.target.value.trim())} />
        <SimpleSelect
          aria-label="Role"
          value={newRole}
          onChange={setNewRole}
          placeholder="Choose a role"
          options={(catalog?.roles ?? []).map((r) => ({ value: r.id, label: r.displayName, hint: r.key }))}
          className="w-56"
        />
        <Button type="submit" variant="outline" disabled={!KEY_PATTERN.test(newKey) || !newRole}>
          <PlusIcon /> Add dimension
        </Button>
      </form>
    </div>
  )
}

function DimensionRow({ dimension, onEdit }: { dimension: ReportCalculationDimension; onEdit: (call: Parameters<ReturnType<typeof useReportEditor>['run']>[0]) => Promise<void> }) {
  const { reportId } = useReportEditor()
  const names = useNames()
  const role = names.role(dimension.roleId)
  const { data: members } = usePublishedMembers(role?.dimensionId ?? null)
  const save = (p: Partial<ReportCalculationDimension>) => onEdit((api) => api.saveCalculationDimension(reportId, { ...dimension, ...p, originalKey: dimension.key }))
  const levels = [...new Set((members?.members ?? []).map((m) => m.levelKey).filter((l): l is string => !!l))]

  const groupingOf = (mode: string): Grouping => {
    switch (mode) {
      case 'level':
        return { mode: 'level', levelKey: levels[0] ?? '' }
      case 'ancestor':
        return { mode: 'ancestor', memberKey: members?.members[0]?.key ?? '' }
      case 'depth':
        return { mode: 'depth', depth: 1 }
      default:
        return { mode: 'member' }
    }
  }

  return (
    <section className="flex flex-wrap items-center gap-3 rounded-lg border bg-card p-3 text-sm" aria-label={dimension.displayName}>
      <div className="flex min-w-48 flex-col">
        <Input aria-label={`Name of ${dimension.key}`} className="h-7" defaultValue={dimension.displayName} onBlur={(e) => e.target.value !== dimension.displayName && void save({ displayName: e.target.value })} />
        <KeyText>
          {dimension.key} · {role?.displayName ?? dimension.roleId}
        </KeyText>
      </div>
      <SimpleSelect
        aria-label="Grouping"
        size="sm"
        value={dimension.grouping.mode}
        onChange={(mode) => void save({ grouping: groupingOf(mode) })}
        options={[
          { value: 'member', label: 'By member' },
          { value: 'level', label: 'By level' },
          { value: 'ancestor', label: 'Under an ancestor' },
          { value: 'depth', label: 'By depth' },
        ]}
        className="w-40"
      />
      {dimension.grouping.mode === 'level' && (
        <SimpleSelect aria-label="Level" size="sm" value={dimension.grouping.levelKey} onChange={(levelKey) => void save({ grouping: { mode: 'level', levelKey } })} options={levels.map((l) => ({ value: l, label: l }))} className="w-36" />
      )}
      {dimension.grouping.mode === 'ancestor' && (
        <SimpleSelect
          aria-label="Ancestor"
          size="sm"
          value={dimension.grouping.memberKey}
          onChange={(memberKey) => void save({ grouping: { mode: 'ancestor', memberKey } })}
          options={(members?.members ?? []).filter((m) => !m.isAssignable).map((m) => ({ value: m.key, label: m.displayName }))}
          className="w-40"
        />
      )}
      {dimension.grouping.mode === 'depth' && (
        <Input aria-label="Depth" type="number" min={0} className="h-7 w-20" defaultValue={dimension.grouping.depth} onBlur={(e) => void save({ grouping: { mode: 'depth', depth: Number(e.target.value) } })} />
      )}
      <label className="flex items-center gap-1.5">
        <Switch checked={dimension.isGroupable} onCheckedChange={(isGroupable) => void save({ isGroupable })} /> Break out
      </label>
      <label className="flex items-center gap-1.5">
        <Switch checked={dimension.isFilterable} onCheckedChange={(isFilterable) => void save({ isFilterable })} /> Filter
      </label>
      <label className="flex items-center gap-1.5">
        <Switch checked={dimension.isEnabled} onCheckedChange={(isEnabled) => void save({ isEnabled })} /> Enabled
      </label>
      <Button size="icon-sm" variant="ghost" className="ml-auto" aria-label={`Delete ${dimension.key}`} onClick={() => void onEdit((api) => api.deleteCalculationDimension(reportId, dimension.key))}>
        <Trash2Icon />
      </Button>
    </section>
  )
}
