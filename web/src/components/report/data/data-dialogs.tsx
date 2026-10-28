import { useState } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { useEditor } from '@/lib/report/store'
import type { Filter } from '@/lib/report/types'
import { KeyText } from '../shared/bits'
import { FilterBuilder } from '../shared/filter-builder'

export function BindingFilterDialog({ sourceKey, bindingId, onClose }: { sourceKey: string; bindingId: string; onClose: () => void }) {
  const { report, mutate } = useEditor()
  const source = report.sources.find((s) => s.key === sourceKey)
  const binding = source?.bindings.find((b) => b.id === bindingId)
  const [filters, setFilters] = useState<Filter[]>(() => structuredClone(binding?.filters ?? []))
  if (!source || !binding) return null
  const fields = report.datasets.find((d) => d.key === source.dataset)?.fields ?? []

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Filter {binding.variable}</DialogTitle>
          <DialogDescription>
            Only records matching every condition feed <KeyText>{binding.variable}</KeyText> from {source.name}.
          </DialogDescription>
        </DialogHeader>
        <FilterBuilder filters={filters} onChange={setFilters} fields={fields.map((f) => ({ value: f, label: f }))} />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={() => {
              mutate((d) => {
                const b = d.sources.find((s) => s.key === sourceKey)?.bindings.find((x) => x.id === bindingId)
                if (b) b.filters = filters.filter((f) => f.field && f.value.trim())
              })
              toast('Filter saved')
              onClose()
            }}
          >
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function ProfilesDialog({ variable, onClose }: { variable: string; onClose: () => void }) {
  const { report, mutate } = useEditor()
  const suppliers = report.sources.filter((s) => s.bindings.some((b) => b.variable === variable))
  const [picks, setPicks] = useState<Record<string, string>>(() =>
    Object.fromEntries(report.profiles.map((p) => [p.key, p.picks[variable] ?? suppliers[0]?.key ?? ''])),
  )

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            Profiles for <span className="font-mono">{variable}</span>
          </DialogTitle>
          <DialogDescription>{suppliers.length} sources supply this variable. Each profile reads it from one of them.</DialogDescription>
        </DialogHeader>
        <ul className="flex flex-col divide-y rounded-lg border">
          {report.profiles.map((p) => (
            <li key={p.key} className="flex flex-col gap-2 px-3 py-2.5">
              <div className="flex items-center gap-2">
                <span className="font-medium">{p.name}</span>
                <KeyText>{p.key}</KeyText>
                {p.key === report.settings.defaultProfile && <Badge variant="secondary">Default</Badge>}
              </div>
              <RadioGroup
                value={picks[p.key]}
                onValueChange={(v) => setPicks((all) => ({ ...all, [p.key]: v as string }))}
                aria-label={`Source for ${variable} in ${p.name}`}
                className="flex flex-col gap-1.5"
              >
                {suppliers.map((s) => (
                  <label key={s.key} className="flex cursor-pointer items-center gap-2">
                    <RadioGroupItem value={s.key} />
                    <span>{s.name}</span>
                  </label>
                ))}
              </RadioGroup>
            </li>
          ))}
        </ul>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={() => {
              mutate((d) => {
                for (const p of d.profiles) if (picks[p.key]) p.picks[variable] = picks[p.key]
              })
              toast('Profiles saved')
              onClose()
            }}
          >
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
