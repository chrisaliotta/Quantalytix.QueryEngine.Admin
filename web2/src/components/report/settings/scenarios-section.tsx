import { useState } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import type { Violation } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { KeyText, Refusal } from '../shared/bits'

/**
 * The scenarios: roles whose members a run reads one at a time (OI-50). A scenario's dimension has exactly one role, and a role is
 * never also a calculation dimension of the report (P9); the server refuses what breaks either.
 */
export function ScenariosSection() {
  const { reportId, report, catalog, run } = useReportEditor()
  const [refusal, setRefusal] = useState<Violation[] | null>(null)
  const chosen = report.scenarios.map((s) => s.roleId)
  const toggle = async (roleId: string, on: boolean) =>
    setRefusal(await run((api) => api.setScenarios(reportId, on ? [...chosen, roleId] : chosen.filter((id) => id !== roleId))))

  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col divide-y rounded-lg border bg-card">
        {(catalog?.roles ?? []).map((role) => (
          <li key={role.id}>
            <label className="flex items-center gap-3 px-3 py-2 text-sm">
              <Checkbox checked={chosen.includes(role.id)} onCheckedChange={(checked) => void toggle(role.id, !!checked)} />
              {role.displayName}
              <KeyText className="ml-auto">{role.key}</KeyText>
            </label>
          </li>
        ))}
      </ul>
      <Refusal violations={refusal} />
    </div>
  )
}
