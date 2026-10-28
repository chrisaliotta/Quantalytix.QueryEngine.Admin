import { CircleCheckIcon, CircleXIcon, ShieldCheckIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useDatasetsApi } from '@/lib/datasets/client'
import type { DatasetTab, DatasetVersion, DatasetValidationReport } from '@/lib/datasets/contract'
import { ProblemBanner, SectionHeader, TAB_LABEL, useApiCall } from './shared'

const TAB_ORDER: DatasetTab[] = ['settings', 'fields', 'grains', 'bindings', 'measures']

export function ReadinessSection({
  version,
  report,
  onReport,
  onOpen,
}: {
  version: DatasetVersion
  report: DatasetValidationReport | null
  onReport: (r: DatasetValidationReport) => void
  onOpen: (tab: DatasetTab, itemId: string | null) => void
}) {
  const api = useDatasetsApi()
  const { run, saving, problem } = useApiCall()
  const canValidate = version.status === 'draft' || version.status === 'validated'

  async function validate() {
    const r = await run(() => api.validateVersion(version.id))
    if (r) onReport(r)
  }

  const grouped = TAB_ORDER.map((tab) => ({ tab, items: (report?.violations ?? []).filter((v) => v.target.area === tab) })).filter((g) => g.items.length)

  return (
    <div className="flex max-w-4xl flex-col gap-4">
      <SectionHeader
        title="Publication readiness"
        help="Every publication rule the API runs on validate and publish. Each issue links to the item at fault."
        action={
          canValidate && (
            <Button onClick={validate} disabled={saving}>
              <ShieldCheckIcon /> {report ? 'Validate again' : 'Validate'}
            </Button>
          )
        }
      />
      {problem && <ProblemBanner problem={problem} />}
      {!canValidate ? (
        <p className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">
          Version {version.versionNumber} is {version.status}. It passed every rule when it was published and can no longer change.
        </p>
      ) : !report ? (
        <p className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">Not checked yet. Validate to run every publication rule against this draft.</p>
      ) : !report.violations.length ? (
        <div className="flex items-center gap-3 rounded-lg border bg-card p-4">
          <CircleCheckIcon aria-hidden className="size-5 text-primary" />
          <div className="flex flex-col">
            <span className="text-sm font-medium">Ready to publish</span>
            <span className="text-xs text-muted-foreground">Every rule passed · checked {new Date(report.checkedAt).toLocaleTimeString()}</span>
          </div>
        </div>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            {report.violations.length} issue{report.violations.length === 1 ? '' : 's'} across {grouped.length} tab{grouped.length === 1 ? '' : 's'} · checked {new Date(report.checkedAt).toLocaleTimeString()}
          </p>
          {grouped.map((g) => (
            <section key={g.tab} aria-labelledby={`rd-${g.tab}`} className="overflow-hidden rounded-lg border bg-card">
              <h3 id={`rd-${g.tab}`} className="flex items-center justify-between border-b bg-muted/40 px-4 py-2 text-sm font-medium">
                {TAB_LABEL[g.tab]}
                <span className="font-mono text-xs text-destructive">{g.items.length}</span>
              </h3>
              <ul className="divide-y">
                {g.items.map((v, i) => (
                  <li key={`${v.code}-${v.target.itemId}-${i}`}>
                    <button type="button" onClick={() => onOpen(v.target.area, v.target.itemId ?? null)} className="flex w-full items-start gap-3 px-4 py-2.5 text-left hover:bg-muted/40">
                      <CircleXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
                      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                        <span className="text-sm text-pretty">{v.message}</span>
                        <span className="flex flex-wrap gap-2 font-mono text-[11px] text-muted-foreground">
                          <span>{v.code}</span>
                          {v.target.itemKey && <span className="text-foreground">{v.target.itemKey}</span>}
                        </span>
                      </span>
                      <span className="shrink-0 text-xs text-primary">Open</span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </>
      )}
    </div>
  )
}
