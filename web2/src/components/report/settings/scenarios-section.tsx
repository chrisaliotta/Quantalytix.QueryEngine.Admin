import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { dimensionForRole, roleInfo, SCENARIO_ROLES } from '@/lib/report/dimensions'
import { useEditor } from '@/lib/report/store'
import { scenarioReaders } from '@/lib/report/scenarios'
import type { ReportScenario, ReportState } from '@/lib/report/types'
import { EditableLabel } from '../shared/editable-label'
import { SimpleSelect } from '../shared/simple-select'

const TH = 'h-9 px-3 text-left align-middle text-xs font-medium whitespace-nowrap text-muted-foreground'
const TD = 'px-2 py-1.5 align-top first:pl-3'
/** Select triggers read as plain text until hovered, focused or open. */
const QUIET =
  'border-transparent bg-transparent shadow-none dark:bg-transparent hover:border-dashed hover:border-border hover:bg-muted/40 dark:hover:bg-muted/40 aria-expanded:border-input data-[popup-open]:border-input'

function joinNames(names: string[]) {
  if (names.length <= 1) return names.join('')
  return `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`
}

/** What happens to runs once a scenario is no longer named. */
function deleteConsequence(state: ReportState, scenario: ReportScenario) {
  const readers = scenarioReaders(state, scenario.role).map((r) => r.source.name)
  const noun = roleInfo(scenario.role)?.memberNoun ?? 'members'
  if (readers.length === 0) return 'No source binds this role, so runs are unaffected.'
  const possessive = readers.length === 1 ? 'its' : 'their'
  return `Runs will stop naming a ${scenario.name.toLowerCase()}, so ${joinNames(readers)} would add ${possessive} ${noun} together.`
}

export function ScenariosSection() {
  const { report, mutate, setReport } = useEditor()
  const undoAction = useUndoAction()
  const [pendingDelete, setPendingDelete] = useState<ReportScenario | null>(null)
  const [justAdded, setJustAdded] = useState<string | null>(null)

  const used = new Set(report.reportScenarios.map((s) => s.role))
  const dimensionRoles = new Set(report.breakoutDims.map((d) => d.role))
  const selectable = SCENARIO_ROLES.filter((r) => !dimensionRoles.has(r.role))
  const available = selectable.filter((r) => !used.has(r.role))

  const dimensionLabel = (role: string) => dimensionForRole(report, role)?.name ?? roleInfo(role)?.name ?? role

  /** Roles this row can switch to: its own plus any not yet used elsewhere. */
  const optionsFor = (role: string) =>
    selectable.filter((r) => r.role === role || !used.has(r.role)).map((r) => ({ value: r.role, label: dimensionLabel(r.role) }))

  const add = () => {
    const next = available[0]
    if (!next) return
    mutate((d) => {
      d.reportScenarios.push({ role: next.role, name: next.name })
    })
    setJustAdded(next.role)
  }

  const changeRole = (from: string, to: string) => {
    if (from === to) return
    mutate((d) => {
      const s = d.reportScenarios.find((x) => x.role === from)
      if (!s) return
      const keptDefaultName = s.name === (roleInfo(from)?.name ?? from)
      s.role = to
      if (keptDefaultName) s.name = roleInfo(to)?.name ?? to
    })
    if (justAdded === from) setJustAdded(to)
  }

  const rename = (role: string, name: string) =>
    mutate((d) => {
      const s = d.reportScenarios.find((x) => x.role === role)
      if (s) s.name = name || roleInfo(role)?.name || role
    })

  const remove = (scenario: ReportScenario) => {
    mutate((d) => {
      d.reportScenarios = d.reportScenarios.filter((s) => s.role !== scenario.role)
    })
    toast(`Removed "${scenario.name}"`, { action: undoAction() })
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground tabular-nums">
          {report.reportScenarios.length} {report.reportScenarios.length === 1 ? 'scenario' : 'scenarios'}
        </p>
        <Button
          variant="outline"
          size="sm"
          onClick={add}
          disabled={available.length === 0}
          title={available.length === 0 ? 'Every scenario dimension is already in use' : undefined}
        >
          <PlusIcon data-icon="inline-start" />
          Add scenario
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <th scope="col" className={TH}>
                Name
              </th>
              <th scope="col" className={TH}>
                Dimension
              </th>
              <th scope="col" className={TH}>
                Read by
              </th>
              <th scope="col" className={TH}>
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {report.reportScenarios.length === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-10 text-center text-muted-foreground">
                  No scenarios. Runs read every member of each source&apos;s roles.
                </td>
              </tr>
            )}
            {report.reportScenarios.map((sc) => {
              const readers = scenarioReaders(report, sc.role)
              return (
                <tr key={sc.role} className="border-b last:border-b-0 hover:bg-muted/30">
                  <td className={`${TD} w-72`}>
                    <EditableLabel
                      aria-label={`Name for ${dimensionLabel(sc.role)}`}
                      value={sc.name}
                      autoEdit={justAdded === sc.role}
                      onCommit={(v) => rename(sc.role, v)}
                      className="font-medium"
                    />
                  </td>
                  <td className={`${TD} w-64`}>
                    <SimpleSelect
                      size="sm"
                      aria-label={`Dimension for ${sc.name}`}
                      className={QUIET}
                      value={sc.role}
                      options={optionsFor(sc.role)}
                      onChange={(role) => changeRole(sc.role, role)}
                    />
                  </td>
                  <td className={TD}>
                    {readers.length === 0 ? (
                      <span className="text-muted-foreground">No source binds this role</span>
                    ) : (
                      <ul className="flex flex-col gap-1">
                        {readers.map(({ source, profiles }) => (
                          <li key={source.key} className="flex flex-wrap items-baseline gap-x-2">
                            <span>{source.name}</span>
                            <span className="text-xs text-muted-foreground">
                              {profiles.length === 0 ? 'No profile picks it' : `Picked by ${joinNames(profiles.map((p) => p.name))}`}
                            </span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </td>
                  <td className={`${TD} w-12 text-right`}>
                    <Button variant="ghost" size="icon-sm" aria-label={`Delete ${sc.name}`} onClick={() => setPendingDelete(sc)}>
                      <Trash2Icon />
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {pendingDelete && (
        <AlertDialog open onOpenChange={(open) => !open && setPendingDelete(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete &ldquo;{pendingDelete.name}&rdquo;?</AlertDialogTitle>
              <AlertDialogDescription>{deleteConsequence(report, pendingDelete)} You can undo right after.</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                onClick={() => {
                  remove(pendingDelete)
                  setPendingDelete(null)
                }}
              >
                Delete scenario
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  )
}
