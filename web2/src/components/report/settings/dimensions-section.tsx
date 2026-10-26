import { CircleAlertIcon, MinusIcon, PlusIcon, SearchIcon, Trash2Icon, TriangleAlertIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import {
  dimensionForRole,
  emptyGrouping,
  fieldError,
  GROUPING_OPTIONS,
  groupingIssue,
  isFlat,
  maxDepth,
  memberLabeler,
  previewGrouping,
  raggedness,
  roleDefaults,
  ROLES,
  type DimNode,
  type Dimension,
} from '@/lib/report/dimensions'
import { dimensionAvailability } from '@/lib/report/scenarios'
import { useEditor } from '@/lib/report/store'
import type { BreakoutDimension, Grouping, GroupingKind } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { EditableLabel } from '../shared/editable-label'
import { SearchCombobox, type ComboOption } from '../shared/search-combobox'
import { SimpleSelect } from '../shared/simple-select'

const MAX_MEMBERS_SHOWN = 50

const ROLE_OPTIONS = ROLES.map((r) => ({ value: r.role, label: r.name, hint: r.role }))

/** Roles a dimension may use: scenario roles already chosen as scenarios are off the list, except the row's own. */
function roleOptionsFor(scenarioRoles: Set<string>, current: string) {
  return ROLE_OPTIONS.filter((o) => o.value === current || !scenarioRoles.has(o.value))
}

const TH = 'h-9 px-2 text-left align-middle text-xs font-medium whitespace-nowrap text-muted-foreground first:pl-3'
const TD = 'px-2 py-1.5 align-top first:pl-3'
const COLS = 10
/** Select triggers read as plain text until hovered, focused or open. */
const QUIET =
  'border-transparent bg-transparent shadow-none dark:bg-transparent hover:border-dashed hover:border-border hover:bg-muted/40 dark:hover:bg-muted/40 aria-expanded:border-input data-[popup-open]:border-input'

function matches(d: BreakoutDimension, q: string) {
  return [d.name, d.aliasKey, roleDefaults(d.role).name].some((s) => s?.toLowerCase().includes(q))
}

export function DimensionsSection() {
  const { report, mutate } = useEditor()
  const [query, setQuery] = useState('')
  const [newKey, setNewKey] = useState<string | null>(null)
  const q = query.trim().toLowerCase()
  const all = report.breakoutDims
  const rows = q ? all.filter((d) => matches(d, q) || d.key === newKey) : all

  function addRow() {
    let n = all.length + 1
    while (all.some((d) => d.key === `dimension_${n}`)) n++
    const key = `dimension_${n}`
    mutate((draft) => {
      draft.breakoutDims.push({ key, name: '', aliasKey: '', role: '', grouping: { kind: 'member' }, groupable: true, filterable: true, enabled: true })
    })
    setNewKey(key)
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative w-full sm:w-72">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            type="search"
            aria-label="Search dimensions"
            placeholder="Search by alias, key or dimension…"
            className="pl-8"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>
        <p className="flex-1 text-sm text-muted-foreground" aria-live="polite">
          {q ? `${rows.length} of ${all.length} dimensions` : `${all.length} ${all.length === 1 ? 'dimension' : 'dimensions'}`}
        </p>
        <Button size="sm" onClick={addRow}>
          <PlusIcon /> New dimension
        </Button>
      </div>

      {all.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-10 text-center">
          <p className="font-medium">No dimensions yet</p>
          <p className="text-muted-foreground">Add one so readers can group or filter the report.</p>
          <Button size="sm" variant="outline" onClick={addRow}>
            <PlusIcon /> New dimension
          </Button>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full min-w-[1280px] caption-bottom text-sm">
            <caption className="sr-only">Report dimensions. Every cell is editable.</caption>
            <thead className="border-b bg-muted/30">
              <tr>
                <th className={cn(TH, 'w-56')}>Dimension</th>
                <th className={cn(TH, 'w-52')}>Alias</th>
                <th className={cn(TH, 'w-48')}>Alias key</th>
                <th className={TH}>Default grouping</th>
                <th className={cn(TH, 'w-16 text-right')}>Rows</th>
                <th className={cn(TH, 'w-24')}>Profiles</th>
                <th className={cn(TH, 'w-20')}>Groupable</th>
                <th className={cn(TH, 'w-20')}>Filterable</th>
                <th className={cn(TH, 'w-20')}>Enabled</th>
                <th className="w-12" aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {rows.map((d) => (
                <DimensionRow key={d.key} dim={d} isNew={d.key === newKey} onDeleted={() => setNewKey((k) => (k === d.key ? null : k))} />
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={COLS} className="px-3 py-8 text-center text-muted-foreground">
                    {`No dimensions match "${query.trim()}".`}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function DimensionRow({ dim: d, isNew, onDeleted }: { dim: BreakoutDimension; isNew: boolean; onDeleted: () => void }) {
  const { report } = useEditor()
  const update = useDimensionMutator(d.key)
  const remove = useRemoveDimension()
  const dimension = dimensionForRole(report, d.role)
  const defaults = roleDefaults(d.role)
  const issue = d.enabled && d.role ? groupingIssue(report, d) : null
  const aliasKeyError = aliasKeyConflict(report.breakoutDims, d.aliasKey, d.key)
  const label = d.name || defaults.name || 'new dimension'

  return (
    <tr
      ref={isNew ? (el) => el?.scrollIntoView({ block: 'nearest' }) : undefined}
      className={cn('border-b transition-colors last:border-b-0 hover:bg-muted/30', isNew && !d.role && 'bg-primary/5', !d.enabled && 'text-muted-foreground')}
    >
      <td className={TD}>
        <div className="flex items-center gap-1.5">
          <SimpleSelect
            size="sm"
            aria-label={`Dimension for ${label}`}
            className={cn(QUIET, 'font-medium')}
            value={d.role}
            placeholder="Choose a dimension"
            options={roleOptionsFor(new Set(report.reportScenarios.map((s) => s.role)), d.role)}
            onChange={(role) =>
              update((x) => {
                const next = applyRoleDefaults(x, role)
                x.name = next.name
                x.aliasKey = next.aliasKey
                x.role = role
                x.grouping = { kind: 'member' }
              })
            }
          />
          {issue && (
            <Tooltip>
              <TooltipTrigger render={<span tabIndex={0} className="shrink-0 rounded-sm text-destructive outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
                <CircleAlertIcon className="size-4" aria-hidden />
                <span className="sr-only">Grouping error: {issue.message}</span>
              </TooltipTrigger>
              <TooltipContent className="max-w-xs">{issue.message}</TooltipContent>
            </Tooltip>
          )}
        </div>
      </td>
      <td className={TD}>
        <EditableLabel
          aria-label={`Alias for ${label}`}
          value={d.name}
          placeholder={defaults.name || 'Choose a dimension'}
          onCommit={(v) => update((x) => void (x.name = v || defaults.name))}
        />
      </td>
      <td className={TD}>
        <EditableLabel
          mono
          aria-label={`Dimension alias key for ${label}`}
          value={d.aliasKey}
          placeholder={defaults.aliasKey || 'Choose a dimension'}
          invalid={!!aliasKeyError}
          title={aliasKeyError ?? undefined}
          normalize={(v) => v.replace(/\s+/g, '_')}
          onCommit={(v) => update((x) => void (x.aliasKey = v || defaults.aliasKey))}
        />
        {aliasKeyError && <p className="px-2 pt-1 text-xs text-destructive">Alias key already in use.</p>}
      </td>
      <td className={TD}>
        {dimension ? (
          <GroupingCell grouping={d.grouping} dimension={dimension} label={label} onChange={(g) => update((x) => void (x.grouping = g))} />
        ) : (
          <span className="flex h-8 items-center text-muted-foreground">Choose a dimension first</span>
        )}
      </td>
      <td className={cn(TD, 'text-right')}>
        <RowCount grouping={d.grouping} dimension={dimension} />
      </td>
      <td className={TD}>
        <ProfilesCell dim={d} />
      </td>
      <td className={TD}>
        <SwitchCell checked={d.groupable} label={`${label} groupable`} onChange={(v) => update((x) => void (x.groupable = v))} />
      </td>
      <td className={TD}>
        <SwitchCell checked={d.filterable} label={`${label} filterable`} onChange={(v) => update((x) => void (x.filterable = v))} />
      </td>
      <td className={TD}>
        <SwitchCell checked={d.enabled} label={`${label} enabled`} onChange={(v) => update((x) => void (x.enabled = v))} />
      </td>
      <td className="w-12 py-1.5 pr-2 text-right align-top">
        <Button
          variant="ghost"
          size="icon-sm"
          className="text-muted-foreground hover:text-destructive"
          aria-label={`Delete ${label}`}
          onClick={() => {
            remove(d)
            onDeleted()
          }}
        >
          <Trash2Icon />
        </Button>
      </td>
    </tr>
  )
}

function SwitchCell({ checked, label, onChange }: { checked: boolean; label: string; onChange: (v: boolean) => void }) {
  return (
    <div className="flex h-8 items-center">
      <Switch size="sm" checked={checked} onCheckedChange={onChange} aria-label={label} />
    </div>
  )
}

function RowCount({ grouping, dimension }: { grouping: Grouping; dimension?: Dimension }) {
  const preview = dimension ? previewGrouping(dimension, grouping) : null
  return (
    <span className="flex h-8 items-center justify-end font-mono tabular-nums">
      {preview ? preview.groups.length.toLocaleString() : <span className="text-muted-foreground">—</span>}
    </span>
  )
}

function ProfilesCell({ dim: d }: { dim: BreakoutDimension }) {
  const { report } = useEditor()
  if (!d.role || report.profiles.length === 0) return <span className="flex h-8 items-center text-muted-foreground">—</span>
  const results = report.profiles.map((p) => ({ p, a: dimensionAvailability(report, d, p) }))
  const available = results.filter((r) => r.a.ok).length
  const all = available === results.length

  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <span
            tabIndex={0}
            className={cn(
              'flex h-8 w-fit items-center gap-1 rounded-sm font-mono tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ring',
              !all && 'text-warning-foreground',
            )}
          />
        }
      >
        {!all && <TriangleAlertIcon className="size-3.5" aria-hidden />}
        {available}/{results.length}
        <span className="sr-only"> profiles can use this dimension</span>
      </TooltipTrigger>
      <TooltipContent className="max-w-xs">
        <ul className="flex flex-col gap-1">
          {results.map(({ p, a }) => (
            <li key={p.key}>
              <span className="font-medium">{p.name}</span>
              {a.ok ? ' — available' : ` — ${a.reason}`}
            </li>
          ))}
        </ul>
      </TooltipContent>
    </Tooltip>
  )
}

function useRemoveDimension() {
  const { mutate } = useEditor()
  const undoAction = useUndoAction()
  return (d: BreakoutDimension) => {
    mutate((draft) => {
      draft.breakoutDims = draft.breakoutDims.filter((x) => x.key !== d.key)
      draft.settings.defaultBreakout = draft.settings.defaultBreakout.filter((k) => k !== d.key)
    })
    toast(`Removed "${d.name || 'dimension'}"`, { action: undoAction() })
  }
}

/** Blank fields, or fields still holding the previous role's defaults, follow the newly chosen role. */
function applyRoleDefaults(current: { name: string; aliasKey: string; role: string }, role: string) {
  const prev = roleDefaults(current.role)
  const next = roleDefaults(role)
  return {
    name: !current.name.trim() || current.name === prev.name ? next.name : current.name,
    aliasKey: !current.aliasKey.trim() || current.aliasKey === prev.aliasKey ? next.aliasKey : current.aliasKey,
  }
}

function aliasKeyConflict(dims: BreakoutDimension[], aliasKey: string, ownKey?: string) {
  const k = aliasKey.trim()
  return k && dims.some((x) => x.key !== ownKey && x.aliasKey === k) ? 'Another dimension already uses this alias key.' : null
}

function useDimensionMutator(key: string) {
  const { mutate } = useEditor()
  return (fn: (d: BreakoutDimension) => void) =>
    mutate((draft) => {
      const d = draft.breakoutDims.find((x) => x.key === key)
      if (d) fn(d)
    })
}

function GroupingCell({ grouping, dimension, label, onChange }: { grouping: Grouping; dimension: Dimension; label: string; onChange: (g: Grouping) => void }) {
  const flat = isFlat(dimension)
  const options = GROUPING_OPTIONS.filter((o) => !flat || o.kind === 'member').map((o) => ({ value: o.kind, label: o.title }))
  const error = fieldError(grouping)

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <div className="w-44 shrink-0">
          <SimpleSelect
            size="sm"
            aria-label={`Group ${label} by`}
            className={QUIET}
            value={grouping.kind}
            options={options}
            disabled={flat}
            onChange={(kind) => onChange(emptyGrouping(kind as GroupingKind))}
          />
        </div>
        <GroupingInput grouping={grouping} dimension={dimension} label={label} invalid={!!error} onChange={onChange} />
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  )
}

function GroupingInput({
  grouping,
  dimension,
  label,
  invalid,
  onChange,
}: {
  grouping: Grouping
  dimension: Dimension
  label: string
  invalid: boolean
  onChange: (g: Grouping) => void
}) {
  if (grouping.kind === 'level') {
    return (
      <div className="w-48">
        <SimpleSelect
          size="sm"
          aria-label={`Level for ${label}`}
          className={cn(QUIET, invalid && 'border-destructive/60')}
          value={grouping.level ?? ''}
          placeholder="Choose a level"
          options={dimension.levels.map((l) => ({ value: l.key, label: l.label }))}
          onChange={(level) => onChange({ kind: 'level', level })}
        />
      </div>
    )
  }

  if (grouping.kind === 'ancestor') {
    return (
      <div className="w-64">
        <SearchCombobox
          aria-label={`Member for ${label}`}
          className={cn(QUIET, 'h-8', invalid && 'border-destructive/60')}
          value={grouping.member ?? ''}
          placeholder="Choose a member"
          searchPlaceholder="Search members…"
          options={parentOptions(dimension)}
          limit={MAX_MEMBERS_SHOWN}
          onChange={(member) => onChange({ kind: 'ancestor', member })}
        />
      </div>
    )
  }

  if (grouping.kind === 'depth') {
    const max = maxDepth(dimension)
    const steps = grouping.steps
    const set = (n: number | null) => onChange({ kind: 'depth', steps: n == null ? null : Math.min(max, Math.max(0, n)) })
    return (
      <div className="flex items-center gap-1">
        <Button type="button" variant="ghost" size="icon-sm" aria-label={`One step fewer for ${label}`} disabled={steps == null || steps <= 0} onClick={() => set((steps ?? 0) - 1)}>
          <MinusIcon />
        </Button>
        <output
          aria-label={`Steps up for ${label}`}
          aria-live="polite"
          className={cn('w-8 text-center font-mono tabular-nums', invalid && 'text-destructive')}
        >
          {steps ?? '—'}
        </output>
        <Button type="button" variant="ghost" size="icon-sm" aria-label={`One step more for ${label}`} disabled={steps != null && steps >= max} onClick={() => set((steps ?? -1) + 1)}>
          <PlusIcon />
        </Button>
        {raggedness(dimension) && (
          <Tooltip>
            <TooltipTrigger render={<span tabIndex={0} className="ml-1 rounded-sm text-warning-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring" />}>
              <TriangleAlertIcon className="size-4" aria-hidden />
              <span className="sr-only">Members sit at different depths</span>
            </TooltipTrigger>
            <TooltipContent className="max-w-xs">Members sit at different depths, so rows may land on different tiers.</TooltipContent>
          </Tooltip>
        )}
      </div>
    )
  }
  return null
}

function parentOptions(dimension: Dimension): ComboOption[] {
  const label = memberLabeler(dimension)
  const levelLabel = new Map(dimension.levels.map((l) => [l.key, l.label]))
  const out: ComboOption[] = []
  const walk = (nodes: DimNode[]) => {
    for (const node of nodes) {
      if (!node.children?.length) continue
      out.push({ value: node.key, label: label(node), hint: node.level ? (levelLabel.get(node.level) ?? node.level) : undefined })
      walk(node.children)
    }
  }
  walk(dimension.members)
  return out
}
