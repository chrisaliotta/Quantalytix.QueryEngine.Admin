import { useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { ReadValue, Td, Th } from '@/components/dimensions/shared'
import type { ApiResult } from '@/lib/api/contract'
import { useDimensionsApi } from '@/lib/dimensions/client'
import { useMappingsApi } from '@/lib/mappings/client'
import { useModelVersion } from '@/lib/models/client'
import { useAppNav } from '@/lib/navigation'
import { useReportVersion } from '@/lib/reports/client'
import type { SnapshotEntry } from '@/lib/snapshots/contract'
import { FrozenVersionNote, Section } from './shared'

type Viewing = { kind: 'calculation_model' | 'report'; versionId: string; title: string } | null

/**
 * Opens the version a snapshot holds of one identity. Datasets, dimensions, mapping sets and analytical bridges open on their own
 * pages, which show a frozen version read-only; report-owned dimensions and bridges open their report. Frozen model and report
 * versions open in a read-only sheet, since their editors edit live only.
 */
export function useEntryBrowser() {
  const nav = useAppNav()
  const dimensions = useDimensionsApi()
  const mappings = useMappingsApi()
  const [viewing, setViewing] = useState<Viewing>(null)

  async function resolve<T>(read: Promise<ApiResult<T>>, go: (data: T) => void) {
    const result = await read
    if (result.ok) go(result.data)
    else toast.error(result.problem.title, { description: result.problem.detail })
  }

  function open(entry: SnapshotEntry, isLive: boolean) {
    const version = isLive ? undefined : entry.versionId
    switch (entry.kind) {
      case 'dataset':
        return nav.dataset(entry.identityId, version)
      case 'dimension':
        return resolve(dimensions.getDimension(entry.identityId), (d) => (d.ownerReport ? nav.reportByKey(d.ownerReport.reportKey) : nav.dimension(d.id, { version })))
      case 'mapping_set':
        return resolve(mappings.getMappingSet(entry.identityId), (s) => nav.mappingSet(s.dimensionId, s.id, version))
      case 'bridge':
        return resolve(mappings.getBridge(entry.identityId), (b) => (b.owner ? nav.reportByKey(b.owner.reportKey) : nav.bridge(b.sourceDimensionId, b.id, version)))
      case 'calculation_model':
        return isLive ? nav.model(entry.key) : setViewing({ kind: entry.kind, versionId: entry.versionId, title: entry.displayName })
      case 'report':
        return isLive ? nav.reportByKey(entry.key) : setViewing({ kind: entry.kind, versionId: entry.versionId, title: entry.displayName })
    }
  }

  const sheet = (
    <Sheet open={!!viewing} onOpenChange={(o) => !o && setViewing(null)}>
      <SheetContent side="right" className="w-full gap-0 sm:max-w-2xl">
        {viewing && (
          <>
            <SheetHeader className="border-b">
              <SheetTitle>{viewing.title}</SheetTitle>
              <SheetDescription>{viewing.kind === 'report' ? 'Report' : 'Calculation model'} as the snapshot holds it.</SheetDescription>
            </SheetHeader>
            <div className="flex flex-col gap-5 overflow-y-auto p-4">
              {viewing.kind === 'report' ? <ReportVersionView versionId={viewing.versionId} /> : <ModelVersionView versionId={viewing.versionId} />}
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  )

  return { open, sheet }
}

function Table({ head, children }: { head: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border bg-card">
      <table className="w-full text-sm">
        <thead className="border-b bg-muted/40">
          <tr>
            {head.map((h) => (
              <Th key={h}>{h}</Th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y">{children}</tbody>
      </table>
    </div>
  )
}

function ModelVersionView({ versionId }: { versionId: string }) {
  const { data: model } = useModelVersion(versionId)
  if (!model) return <p className="text-sm text-muted-foreground">Loading the model version…</p>
  return (
    <>
      <FrozenVersionNote version={model} />
      <div className="grid gap-4 sm:grid-cols-3">
        <ReadValue label="Key" mono>
          {model.key}
        </ReadValue>
        <ReadValue label="Currency">{model.currencyCode}</ReadValue>
        <ReadValue label="Calendar">{model.calendarKey}</ReadValue>
        <ReadValue label="Formula language">{model.formulaLanguage}</ReadValue>
        <ReadValue label="Attribution">{model.attributionMethod}</ReadValue>
        <ReadValue label="Enabled">{model.isEnabled ? 'Yes' : 'No'}</ReadValue>
      </div>
      <Section title="Variables" count={model.variables.length}>
        <Table head={['Key', 'Name', 'Format']}>
          {model.variables.map((v) => (
            <tr key={v.key}>
              <Td className="font-mono text-xs">{v.key}</Td>
              <Td>{v.displayName}</Td>
              <Td className="text-muted-foreground">{v.displayFormat}</Td>
            </tr>
          ))}
        </Table>
      </Section>
      <Section title="Calculated measures" count={model.measures.length}>
        <Table head={['Key', 'Name', 'Formula']}>
          {model.measures.map((m) => (
            <tr key={m.key}>
              <Td className="font-mono text-xs">{m.key}</Td>
              <Td>{m.displayName}</Td>
              <Td className="font-mono text-xs text-muted-foreground">{m.formula}</Td>
            </tr>
          ))}
        </Table>
      </Section>
      <Section title="Parameters" count={model.parameters.length}>
        <Table head={['Key', 'Name', 'Type']}>
          {model.parameters.map((p) => (
            <tr key={p.key}>
              <Td className="font-mono text-xs">{p.key}</Td>
              <Td>{p.displayName}</Td>
              <Td className="text-muted-foreground">{p.valueType}</Td>
            </tr>
          ))}
        </Table>
      </Section>
    </>
  )
}

function ReportVersionView({ versionId }: { versionId: string }) {
  const { data: report } = useReportVersion(versionId)
  if (!report) return <p className="text-sm text-muted-foreground">Loading the report version…</p>
  const { settings } = report
  return (
    <>
      <FrozenVersionNote version={report} />
      <div className="grid gap-4 sm:grid-cols-3">
        <ReadValue label="Key" mono>
          {report.key}
        </ReadValue>
        <ReadValue label="Type">{settings.reportType}</ReadValue>
        <ReadValue label="Layout">{settings.layout}</ReadValue>
        <ReadValue label="Periodicity">{settings.periodicity}</ReadValue>
        <ReadValue label="Default profile" mono>
          {settings.defaultProfileKey}
        </ReadValue>
        <ReadValue label="Enabled">{report.isEnabled ? 'Yes' : 'No'}</ReadValue>
      </div>
      <Section title="Rows" count={report.rows.length}>
        <Table head={['Key', 'Label', 'Kind']}>
          {report.rows.map((r) => (
            <tr key={r.key}>
              <Td className="font-mono text-xs">{r.key}</Td>
              <Td>{r.label}</Td>
              <Td className="text-muted-foreground">{r.kind}</Td>
            </tr>
          ))}
        </Table>
      </Section>
      <Section title="Sources" count={report.sources.length}>
        <Table head={['Key', 'Name', 'Method']}>
          {report.sources.map((s) => (
            <tr key={s.key}>
              <Td className="font-mono text-xs">{s.key}</Td>
              <Td>{s.displayName}</Td>
              <Td className="text-muted-foreground">{s.method}</Td>
            </tr>
          ))}
        </Table>
      </Section>
      <Section title="Columns" count={report.columns.length}>
        <Table head={['Key', 'Metric', 'Kind']}>
          {report.columns.map((c) => (
            <tr key={c.key}>
              <Td className="font-mono text-xs">{c.key}</Td>
              <Td className="font-mono text-xs">{c.metricKey}</Td>
              <Td className="text-muted-foreground">{c.valueKind}</Td>
            </tr>
          ))}
        </Table>
      </Section>
    </>
  )
}
