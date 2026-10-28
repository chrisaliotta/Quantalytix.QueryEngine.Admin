import { ArrowRightIcon, CheckIcon, ChevronRightIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Label } from '@/components/ui/label'
import { readiness, type ReadinessStep, type StepId } from '@/lib/report/coverage'
import { useEditor, type SettingsSectionId, type Tab } from '@/lib/report/store'
import { cn } from '@/lib/utils'
import { StatusDot } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

type Jump = { label: string } & ({ tab: Tab } | { section: SettingsSectionId })

const STEP_JUMP: Partial<Record<StepId, Jump>> = {
  rows: { label: 'Go to Rows', tab: 'rows' },
  sources: { label: 'Go to Data', tab: 'data' },
  variables: { label: 'Go to Data', tab: 'data' },
  items: { label: 'Go to Data', tab: 'data' },
  profiles: { label: 'Go to Profiles', section: 'profiles' },
  dimensions: { label: 'Go to Dimensions', section: 'breakout' },
  columns: { label: 'Go to Columns', tab: 'columns' },
}

const STATUS_TEXT = { pass: 'Passes', warn: 'Warning', fail: 'Blocks enabling' } as const

export function usePublishStatus() {
  const { report, ui } = useEditor()
  const profileKey = ui.readinessProfile ?? ui.activeProfile
  const steps = readiness(report, profileKey)
  const ready = steps.find((s) => s.id === 'ready')!
  const checks = steps.filter((s) => s.id !== 'ready')
  const overall: ReadinessStep['status'] =
    ready.status === 'fail' ? 'fail' : checks.some((s) => s.status === 'warn') ? 'warn' : 'pass'
  return { profileKey, ready, checks, overall, canEnable: ready.status !== 'fail' }
}

export function PublishSection() {
  const { report, ui, setUi } = useEditor()
  const { profileKey, checks } = usePublishStatus()

  return (
    <div className="flex flex-col gap-6">
      <section aria-label="Diagnostics" className="flex flex-col gap-3">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <p className="text-muted-foreground">Checked against your unsaved changes.</p>
          <div className="flex items-center gap-2">
            <Label htmlFor="diag-profile" className="text-muted-foreground">
              Profile
            </Label>
            <SimpleSelect
              id="diag-profile"
              size="sm"
              className="w-40"
              value={profileKey}
              onChange={(v) => setUi({ readinessProfile: v === ui.activeProfile ? null : v })}
              options={report.profiles.map((p) => ({ value: p.key, label: p.key }))}
            />
          </div>
        </div>

        <ul className="flex flex-col divide-y rounded-lg border bg-card">
          {checks.map((step) => (
            <DiagnosticRow key={step.id} step={step} />
          ))}
        </ul>
      </section>
    </div>
  )
}

function DiagnosticRow({ step }: { step: ReadinessStep }) {
  const { setUi, openDialog } = useEditor()
  const jump = STEP_JUMP[step.id]
  const go = () =>
    jump && setUi('tab' in jump ? { view: 'editor', tab: jump.tab } : { view: 'settings', settingsSection: jump.section })

  const hasIssues = step.status !== 'pass' && step.issues.length > 0

  const summary = (
    <>
      {hasIssues ? (
        <ChevronRightIcon
          aria-hidden
          className="size-4 shrink-0 text-muted-foreground transition-transform group-data-[panel-open]:rotate-90"
        />
      ) : (
        <span aria-hidden className="size-4 shrink-0" />
      )}
      {step.status === 'pass' ? (
        <CheckIcon aria-hidden className="size-4 shrink-0 text-primary" />
      ) : (
        <StatusDot status={step.status} className="mx-1" />
      )}
      <span className="font-medium">{step.label}</span>
      <span
        className={cn(
          'text-sm',
          step.status === 'pass' && 'text-muted-foreground',
          step.status === 'warn' && 'text-warning-foreground',
          step.status === 'fail' && 'text-destructive',
        )}
      >
        {STATUS_TEXT[step.status]}
        {hasIssues && ` · ${step.issues.length} ${step.issues.length === 1 ? 'issue' : 'issues'}`}
      </span>
    </>
  )

  return (
    <li>
      <Collapsible>
        <div className="flex items-center justify-between gap-3 px-2 py-1.5">
          {hasIssues ? (
            <CollapsibleTrigger className="group flex min-w-0 flex-1 items-center gap-2.5 rounded-md px-2 py-1.5 text-left outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring">
              {summary}
            </CollapsibleTrigger>
          ) : (
            <div className="flex min-w-0 flex-1 items-center gap-2.5 px-2 py-1.5">{summary}</div>
          )}
          {step.status !== 'pass' && jump && (
            <Button size="sm" variant="ghost" onClick={go}>
              {jump.label}
              <ArrowRightIcon data-icon="inline-end" />
            </Button>
          )}
        </div>
        {hasIssues && (
          <CollapsibleContent>
            <ul className="flex flex-col gap-1.5 pr-4 pb-3 pl-16">
              {step.issues.map((issue) => (
                <li key={issue.message} className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
                  <span className="text-pretty">{issue.message}</span>
                  {issue.sourceKey && (
                    <Button size="xs" variant="outline" onClick={() => openDialog({ type: 'coverage', sourceKey: issue.sourceKey! })}>
                      Review
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </CollapsibleContent>
        )}
      </Collapsible>
    </li>
  )
}
