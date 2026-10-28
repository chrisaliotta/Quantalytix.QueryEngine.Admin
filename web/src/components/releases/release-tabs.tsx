import { CheckCircle2Icon, PlayIcon, TriangleAlertIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAppNav } from '@/lib/navigation'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { EmptyState, TabIntro } from '@/components/dimensions/shared'
import { useLookup, useReleaseDiff, useReleaseList } from '@/lib/releases/client'
import { RELEASE_RULES, RULE_LABEL, type ReleaseCheckReport, type ReleaseDetail, type ReleaseDiff, type ReleaseViolation } from '@/lib/releases/contract'
import { cn } from '@/lib/utils'
import { DIFF_TONE, LinkText, RelTime, VersionTag } from './shared'

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`

export function diffSummary(d: ReleaseDiff) {
  return `${plural(d.changed.length, 'dataset changes version', 'datasets change version')}, ${d.added.length} added, ${d.removed.length} removed`
}

/** Added / removed / changed rows, colored consistently, each side linking to its dataset version. */
export function DiffList({ diff, compact }: { diff: ReleaseDiff; compact?: boolean }) {
  const lookup = useLookup()
  const nav = useAppNav()
  const ds = (id: string) => lookup.dataset(id)
  const ver = (datasetId: string, versionId: string) => {
    const v = lookup.version(versionId)
    return v ? <VersionTag n={v.versionNumber} status={v.status} showStatus={!compact} onClick={() => nav.dataset(datasetId, versionId)} /> : <span className="font-mono text-xs">?</span>
  }
  const reportKey = (id: string) => lookup.report(id)?.key ?? id

  const groups = [
    { title: 'Added', tone: DIFF_TONE.added, mark: '+', rows: diff.added.map((a) => ({ id: a.datasetId, body: ver(a.datasetId, a.versionId), note: null as string | null })) },
    {
      title: 'Removed',
      tone: DIFF_TONE.removed,
      mark: '−',
      rows: diff.removed.map((r) => ({ id: r.datasetId, body: ver(r.datasetId, r.versionId), note: r.readBy.length ? `read by ${r.readBy.map(reportKey).join(', ')}` : null })),
    },
    {
      title: 'Version changes',
      tone: DIFF_TONE.changed,
      mark: '~',
      rows: diff.changed.map((c) => ({
        id: c.datasetId,
        body: (
          <span className="inline-flex items-center gap-1.5">
            {ver(c.datasetId, c.fromVersionId)}
            <span aria-label="to" className="text-muted-foreground">
              →
            </span>
            {ver(c.datasetId, c.toVersionId)}
          </span>
        ),
        note: null,
      })),
    },
  ]

  return (
    <div className="flex flex-col gap-4">
      {groups.map((g) =>
        !g.rows.length && compact ? null : (
          <div key={g.title} className="flex flex-col gap-1.5">
            <h3 className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
              <span className={cn('font-mono', g.tone)}>{g.mark}</span>
              {g.title}
              <span className="font-mono tabular-nums">{g.rows.length}</span>
            </h3>
            {g.rows.length ? (
              <ul className={cn('flex flex-col divide-y rounded-md border', !compact && 'bg-card')}>
                {g.rows.map((r) => (
                  <li key={r.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
                    <span className={cn('w-3 font-mono', g.tone)} aria-hidden>
                      {g.mark}
                    </span>
                    <LinkText mono onClick={() => nav.dataset(r.id)}>
                      {ds(r.id)?.key ?? r.id}
                    </LinkText>
                    {r.body}
                    {r.note && (
                      <span className="ml-auto inline-flex items-center gap-1 text-xs text-destructive">
                        <TriangleAlertIcon aria-hidden className="size-3.5" />
                        {r.note}
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="px-1 text-xs text-muted-foreground">None.</p>
            )}
          </div>
        ),
      )}
      {!compact && (
        <details className="rounded-md border bg-card px-3 py-2 text-sm">
          <summary className="cursor-pointer text-muted-foreground">
            Unchanged <span className="font-mono tabular-nums">{diff.unchanged}</span>
          </summary>
          <p className="pt-2 text-xs text-muted-foreground">{plural(diff.unchanged, 'dataset pins', 'datasets pin')} the same version in both releases.</p>
        </details>
      )}
    </div>
  )
}

/* ───────────────────────── Changes ───────────────────────── */

export function ChangesTab({ detail, compareTo, onCompareTo, unsaved }: { detail: ReleaseDetail; compareTo: string | null; onCompareTo: (id: string) => void; unsaved: boolean }) {
  const { data: list } = useReleaseList()
  const thisId = detail.release.id
  const others = (list ?? []).filter((r) => r.id !== thisId)
  const fallback = detail.isActive ? detail.activations[0]?.previousReleaseId : others.find((r) => r.isActive)?.id
  const otherId = compareTo ?? fallback ?? null
  const { data: diff } = useReleaseDiff(otherId, thisId)
  const other = others.find((r) => r.id === otherId)

  return (
    <div className="flex flex-col gap-5">
      <TabIntro
        action={
          <div className="flex items-center gap-2">
            <label htmlFor="compare-to" className="text-xs text-muted-foreground">
              Compare against
            </label>
            <SimpleSelect
              id="compare-to"
              size="sm"
              className="w-56"
              value={otherId ?? ''}
              onChange={onCompareTo}
              placeholder="Choose a release"
              options={others.map((r) => ({ value: r.id, label: r.isActive ? `${r.key} (active)` : r.key, hint: r.status }))}
            />
          </div>
        }
      >
        {other?.isActive
          ? 'What activating this release would switch: datasets it adds, removes or moves to another version compared with the active release.'
          : detail.isActive && otherId === fallback
            ? 'What this release changed when it was activated: compared with the release it replaced.'
            : 'Datasets this release adds, removes or moves to another version compared with the chosen release.'}
        {unsaved && <span className="ml-1 font-medium text-warning-foreground">Includes unsaved manifest edits.</span>}
      </TabIntro>
      {!otherId ? (
        <EmptyState title="Nothing to compare with">There is no other release yet.</EmptyState>
      ) : !diff ? (
        <p className="text-sm text-muted-foreground">Comparing…</p>
      ) : (
        <>
          <p className="text-sm">
            <span className="font-mono">{other?.key}</span> <span className="text-muted-foreground">→</span> <span className="font-mono">{detail.release.key}</span>: {diffSummary(diff)}.
          </p>
          <DiffList diff={diff} />
        </>
      )}
    </div>
  )
}

/* ───────────────────────── Checks ───────────────────────── */

export function ChecksPanel({ report, running, onRun, onTarget }: { report: ReleaseCheckReport | null; running: boolean; onRun: () => void; onTarget: (v: ReleaseViolation) => void }) {
  const lookup = useLookup()
  const byRule = new Map<string, ReleaseViolation[]>()
  for (const v of report?.violations ?? []) byRule.set(v.code, [...(byRule.get(v.code) ?? []), v])
  const linkLabel = ({ target: t, datasetId }: ReleaseViolation) => {
    if (t?.area === 'report') return `Open ${lookup.report(t.itemId)?.key ?? 'report'} · ${t.itemKey}`
    if (t && t.area !== 'dataset') return `Open ${lookup.identity(t.itemId)?.key ?? 'identity'}`
    if (datasetId) return 'Show manifest row'
    return null
  }

  return (
    <div className="flex flex-col gap-5">
      <TabIntro
        action={
          <Button size="sm" variant="outline" onClick={onRun} disabled={running}>
            <PlayIcon />
            {running ? 'Running…' : 'Run checks'}
          </Button>
        }
      >
        Runs the release rules in the order the platform does. Validate and Publish run them first and stop on any violation.
      </TabIntro>
      {!report ? (
        <EmptyState title="No checks run yet">Run the checks to see whether this manifest would validate.</EmptyState>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            {report.violations.length ? (
              <span className="inline-flex items-center gap-1.5 font-medium text-destructive">
                <TriangleAlertIcon aria-hidden className="size-4" />
                {plural(report.violations.length, 'violation')} across {plural(byRule.size, 'rule')}
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 font-medium text-success-foreground">
                <CheckCircle2Icon aria-hidden className="size-4" />
                All {RELEASE_RULES.length} checks pass
              </span>
            )}
            {report.unsaved && <span className="rounded-full border border-warning/50 bg-warning-muted px-2 py-0.5 text-xs text-warning-foreground">unsaved</span>}
            <span className="text-xs text-muted-foreground">
              Ran <RelTime iso={report.checkedAt} />
            </span>
          </div>
          <ol className="flex flex-col divide-y rounded-lg border bg-card">
            {RELEASE_RULES.map((rule) => {
              const vs = byRule.get(rule) ?? []
              return (
                <li key={rule} className="flex flex-col gap-2 px-4 py-3">
                  <div className="flex items-center gap-2 text-sm">
                    {vs.length ? <TriangleAlertIcon aria-label="Failed" className="size-4 text-destructive" /> : <CheckCircle2Icon aria-label="Passed" className="size-4 text-success-foreground" />}
                    <span className={cn(vs.length && 'font-medium')}>{RULE_LABEL[rule]}</span>
                    <code className="font-mono text-[11px] text-muted-foreground">{rule}</code>
                  </div>
                  {vs.length > 0 && (
                    <ul className="flex flex-col gap-1.5 pl-6">
                      {vs.map((v) => {
                        const label = linkLabel(v)
                        return (
                          <li key={v.message} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-sm">
                            <span className="text-pretty">{v.message}</span>
                            {label && (
                              <button type="button" onClick={() => onTarget(v)} className="text-xs text-primary underline-offset-2 hover:underline">
                                {label}
                              </button>
                            )}
                          </li>
                        )
                      })}
                    </ul>
                  )}
                </li>
              )
            })}
          </ol>
        </>
      )}
    </div>
  )
}
