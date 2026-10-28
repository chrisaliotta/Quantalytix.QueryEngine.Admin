import { Badge } from '@/components/ui/badge'
import type { ReportVariableResolution } from '@/lib/reports/contract'
import { useNames, useReportEditor } from '../editor-state'
import { KeyText, SectionLabel } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

const STATUS_LABEL: Record<ReportVariableResolution['status'], string> = {
  ok: 'Supplied',
  shadowed: 'Supplied; other sources too',
  gap: 'Source does not reach this row',
  unresolved: 'No source picked',
  na: 'Not read by the report',
}

/** What a data row shows under a profile: for each variable, the source the profile reads it from, as the server resolves it. */
export function RowResolution({ rowKey }: { rowKey: string }) {
  const { report, diagnostics, ui, setUi } = useReportEditor()
  const names = useNames()
  const profileKey = ui.profileKey ?? report.settings.defaultProfileKey ?? report.profiles[0]?.key
  const resolution = profileKey ? diagnostics?.resolution[profileKey] : undefined
  if (!profileKey) return null

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <SectionLabel>What this row shows</SectionLabel>
        <SimpleSelect
          aria-label="Profile"
          size="sm"
          value={profileKey}
          onChange={(key) => setUi({ profileKey: key })}
          options={report.profiles.map((p) => ({ value: p.key, label: p.displayName }))}
          className="w-44"
        />
      </div>
      <ul className="flex flex-col divide-y rounded-lg border">
        {Object.entries(resolution ?? {})
          .filter(([, r]) => r.status !== 'na')
          .map(([variableKey, r]) => {
            const status = r.status === 'gap' && !r.rowGaps.includes(rowKey) ? 'ok' : r.status
            return (
              <li key={variableKey} className="flex items-center gap-2 px-3 py-2 text-sm">
                <span>{names.variable(variableKey)?.displayName ?? variableKey}</span>
                <KeyText>{variableKey}</KeyText>
                <span className="ml-auto text-muted-foreground">{r.sourceKey ? report.sources.find((s) => s.key === r.sourceKey)?.displayName ?? r.sourceKey : '—'}</span>
                <Badge variant={status === 'ok' || status === 'shadowed' ? 'outline' : 'destructive'}>{STATUS_LABEL[status]}</Badge>
              </li>
            )
          })}
      </ul>
    </section>
  )
}
