import { PlusIcon, XIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { ReportSource, ReportSourceRule, Violation } from '@/lib/reports/contract'
import { usePublishedMembers } from '@/lib/tenant/client'
import { useNames, useReportEditor } from '../editor-state'
import { KeyText, Refusal, factorLabel } from '../shared/bits'
import { SearchCombobox } from '../shared/search-combobox'
import { Segmented } from '../shared/segmented'

/**
 * The rules one bridge source adds into one data row: an item, or with `descendants` every item under it, times a factor. The
 * factor is the only number a rule carries; -1 on an item a folder rule already includes takes it back out (OI-52).
 */
export function RowRules({ source, rowKey }: { source: ReportSource; rowKey: string }) {
  const { reportId, run } = useReportEditor()
  const names = useNames()
  const role = names.role(source.roleId)
  const { data: members } = usePublishedMembers(role?.dimensionId ?? null)
  const stored = source.rules[rowKey] ?? []
  const [rules, setRules] = useState<ReportSourceRule[]>(stored)
  const [picked, setPicked] = useState('')
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const memberOf = (key: string) => members?.members.find((m) => m.key === key)

  const add = () => {
    const member = memberOf(picked)
    if (!member || rules.some((r) => r.itemKey === picked)) return
    setRules([...rules, { itemKey: picked, selection: member.isAssignable ? 'member' : 'descendants', factor: 1 }])
    setPicked('')
  }
  const patch = (itemKey: string, p: Partial<ReportSourceRule>) => setRules(rules.map((r) => (r.itemKey === itemKey ? { ...r, ...p } : r)))
  const apply = async () => setRefusal(await run((api) => api.setRowRules(reportId, source.key, rowKey, rules)))

  return (
    <div className="flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-sm font-medium">{source.displayName}</span>
        <KeyText>{role?.displayName ?? source.roleId}</KeyText>
      </div>
      <ul className="flex flex-col gap-1.5">
        {rules.map((rule) => (
          <li key={rule.itemKey} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-24">{memberOf(rule.itemKey)?.displayName ?? rule.itemKey}</span>
            <KeyText>{rule.itemKey}</KeyText>
            <Segmented
              aria-label={`Selection of ${rule.itemKey}`}
              value={rule.selection}
              onChange={(selection) => patch(rule.itemKey, { selection })}
              options={[
                { value: 'member', label: 'Item' },
                { value: 'descendants', label: 'Item and below' },
              ]}
            />
            <Input
              type="number"
              step="any"
              aria-label={`Factor of ${rule.itemKey}`}
              className="h-7 w-20"
              value={rule.factor}
              onChange={(e) => patch(rule.itemKey, { factor: Number(e.target.value) })}
            />
            <span className="text-xs text-muted-foreground">{factorLabel(rule.factor)}</span>
            <Button size="icon-xs" variant="ghost" aria-label={`Remove ${rule.itemKey}`} onClick={() => setRules(rules.filter((r) => r !== rule))}>
              <XIcon />
            </Button>
          </li>
        ))}
      </ul>
      <div className="flex items-center gap-2">
        <SearchCombobox
          aria-label="Item to add"
          value={picked}
          onChange={setPicked}
          placeholder="Add an item…"
          options={(members?.members ?? []).map((m) => ({ value: m.key, label: m.displayName, hint: m.key }))}
          className="w-64"
        />
        <Button size="sm" variant="outline" onClick={add} disabled={!picked}>
          <PlusIcon /> Add
        </Button>
        <Button size="sm" className="ml-auto" onClick={() => void apply()} disabled={JSON.stringify(rules) === JSON.stringify(stored)}>
          Apply
        </Button>
      </div>
      <Refusal violations={refusal} />
    </div>
  )
}
