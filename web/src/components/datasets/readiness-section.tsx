import { CircleCheckIcon, CircleXIcon } from 'lucide-react'
import type { DatasetTab, DatasetVersion, DatasetViolation, Violation } from '@/lib/datasets/contract'
import { SectionHeader, TAB_LABEL } from './shared'

const TAB_ORDER: DatasetTab[] = ['settings', 'fields', 'grains', 'bindings', 'measures']

/** The commit rules' findings on the pending edits of the live version; saving is blocked until none remains. */
export function ReadinessSection({ version, findings, onOpen }: { version: DatasetVersion; findings: Violation[]; onOpen: (tab: DatasetTab, itemId: string | null) => void }) {
  const violations = findings as DatasetViolation[]
  const grouped = TAB_ORDER.map((tab) => ({ tab, items: violations.filter((v) => v.target.area === tab) })).filter((g) => g.items.length)

  return (
    <div className="flex max-w-4xl flex-col gap-4">
      <SectionHeader title="Rule findings" help="Every commit rule the API runs on your unsaved edits. Saving is blocked until none remains; each finding links to the item at fault." />
      {!version.isLive ? (
        <p className="rounded-lg border bg-card p-4 text-sm text-muted-foreground">A frozen version passed every rule while it was live and never changes.</p>
      ) : !violations.length ? (
        <div className="flex items-center gap-3 rounded-lg border bg-card p-4">
          <CircleCheckIcon aria-hidden className="size-5 text-primary" />
          <div className="flex flex-col">
            <span className="text-sm font-medium">No findings</span>
            <span className="text-xs text-muted-foreground">The live version and your unsaved edits pass every rule.</span>
          </div>
        </div>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            {violations.length} finding{violations.length === 1 ? '' : 's'} across {grouped.length} tab{grouped.length === 1 ? '' : 's'}
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
