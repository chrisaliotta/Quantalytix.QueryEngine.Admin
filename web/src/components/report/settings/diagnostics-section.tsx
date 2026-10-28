import type { ReportDiagnosticsStepName } from '@/lib/reports/contract'
import { useReportEditor } from '../editor-state'
import { StatusDot } from '../shared/bits'

const STEP_LABEL: Record<ReportDiagnosticsStepName, string> = {
  rows: 'Rows',
  sources: 'Sources',
  variables: 'Variables',
  items: 'Items mapped',
  profiles: 'Profiles',
  dimensions: 'Dimensions',
  columns: 'Columns',
}

/**
 * The server's diagnostics of the report as it stands, pending edits included (OI-05): each step with the report rules' findings,
 * and coverage warnings, which do not keep the report from being enabled.
 */
export function DiagnosticsSection() {
  const { diagnostics } = useReportEditor()
  if (!diagnostics) return <p className="text-sm text-muted-foreground">Checking…</p>
  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm">{diagnostics.ready ? 'Every report rule passes; the report can be enabled.' : 'Fix the failing steps before enabling the report.'}</p>
      <ul className="flex flex-col divide-y rounded-lg border bg-card">
        {diagnostics.steps.map((step) => (
          <li key={step.step} className="flex flex-col gap-1.5 px-3 py-2.5">
            <div className="flex items-center gap-2 text-sm font-medium">
              <StatusDot status={step.status} />
              {STEP_LABEL[step.step]}
            </div>
            {step.violations.length > 0 && (
              <ul className="flex flex-col gap-1 pl-4 text-sm text-muted-foreground">
                {step.violations.map((v, i) => (
                  <li key={i}>{v.message}</li>
                ))}
              </ul>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
