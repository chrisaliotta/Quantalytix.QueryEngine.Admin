import { TriangleAlertIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { useModels } from '@/lib/model/store'
import { useEditor } from '@/lib/report/store'
import type { ReportState } from '@/lib/report/types'
import { KeyText } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

interface VariableLoss {
  key: string
  name: string
  uses: string[]
}

function plural(n: number, one: string, many = `${one}s`) {
  return `${n} ${n === 1 ? one : many}`
}

/** Every variable the report references, with a summary of where it is used. */
function variableUsage(report: ReportState) {
  const usage = new Map<string, { bindings: number; picks: number; scopes: number; columns: number }>()
  const entry = (key: string) => {
    let e = usage.get(key)
    if (!e) usage.set(key, (e = { bindings: 0, picks: 0, scopes: 0, columns: 0 }))
    return e
  }
  for (const s of report.sources) for (const b of s.bindings) entry(b.variable).bindings++
  for (const p of report.profiles) for (const v of Object.keys(p.picks)) entry(v).picks++
  for (const sc of report.rowScopes) entry(sc.variable).scopes++
  for (const c of report.columns) if (c.kind === 'variable') entry(c.metric).columns++
  return usage
}

export function ChangeModelDialog({ onClose }: { onClose: () => void }) {
  const { report, mutate } = useEditor()
  const { models, getModel } = useModels()
  const current = getModel(report.settings.model)
  const [next, setNext] = useState(report.settings.model)
  const target = getModel(next)
  const unchanged = next === report.settings.model

  const targetKeys = new Set(target?.variables.map((v) => v.key) ?? [])
  const nameOf = (key: string) =>
    current?.variables.find((v) => v.key === key)?.name ?? report.variables.find((v) => v.key === key)?.name ?? key

  const losses: VariableLoss[] = unchanged
    ? []
    : [...variableUsage(report)]
        .filter(([key]) => !targetKeys.has(key))
        .map(([key, u]) => ({
          key,
          name: nameOf(key),
          uses: [
            u.bindings && plural(u.bindings, 'source binding'),
            u.picks && plural(u.picks, 'profile pick'),
            u.scopes && plural(u.scopes, 'scope'),
            u.columns && plural(u.columns, 'column'),
          ].filter(Boolean) as string[],
        }))

  const apply = () => {
    const lost = new Set(losses.map((l) => l.key))
    mutate((d) => {
      d.settings.model = next
      for (const s of d.sources) s.bindings = s.bindings.filter((b) => !lost.has(b.variable))
      for (const p of d.profiles) for (const k of lost) delete p.picks[k]
      d.rowScopes = d.rowScopes.filter((sc) => !lost.has(sc.variable))
      d.columns = d.columns.filter((c) => !(c.kind === 'variable' && lost.has(c.metric)))
      d.variables = d.variables.filter((v) => !lost.has(v.key))
    })
    toast(`Report type changed to ${target?.name ?? next}`, {
      description: lost.size ? `${plural(lost.size, 'variable')} unmapped and removed.` : 'Every variable carried over.',
    })
    onClose()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Change report type</DialogTitle>
          <DialogDescription className="text-pretty">
            The report type is the calculation model its variables and measures come from. Variables carry over when the
            new model has a variable with the same key.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="change-model">Report type</Label>
          <SimpleSelect
            id="change-model"
            value={next}
            onChange={setNext}
            options={models.map((m) => ({ value: m.key, label: m.name, hint: m.key }))}
          />
        </div>

        {!unchanged && losses.length === 0 && (
          <p className="text-sm text-muted-foreground">Every variable this report uses has a match in {target?.name}.</p>
        )}

        {losses.length > 0 && (
          <div role="alert" className="flex flex-col gap-3 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm">
            <p className="flex items-start gap-2 font-medium text-destructive">
              <TriangleAlertIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span className="text-pretty">
                {plural(losses.length, 'variable')} {losses.length === 1 ? 'has' : 'have'} no match in {target?.name} and
                will be unmapped and lost.
              </span>
            </p>
            <ul className="flex max-h-56 flex-col gap-2 overflow-auto">
              {losses.map((l) => (
                <li key={l.key} className="flex flex-col gap-0.5">
                  <span className="flex min-w-0 items-baseline gap-2">
                    <span className="truncate font-medium text-foreground">{l.name}</span>
                    <KeyText className="text-xs">{l.key}</KeyText>
                  </span>
                  <span className="text-xs text-muted-foreground">{l.uses.join(' · ')}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button variant={losses.length ? 'destructive' : 'default'} disabled={unchanged} onClick={apply}>
            {losses.length ? `Change type and remove ${plural(losses.length, 'variable')}` : 'Change type'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
