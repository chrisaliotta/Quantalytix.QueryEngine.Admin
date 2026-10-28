import { useState } from 'react'
import { AlertTriangleIcon, ChevronRightIcon, CircleCheckIcon, CircleUserIcon, PencilIcon, PlusIcon, StarIcon, Trash2Icon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { getProfile, pickedSource, profileGaps, suppliersOf, unresolvedVariables, variableUsage } from '@/lib/report/profiles'
import { profileMix, profileScenarios } from '@/lib/report/scenarios'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import type { Profile } from '@/lib/report/types'
import { cn } from '@/lib/utils'
import { KeyText, WARN_CHIP } from '../shared/bits'
import { SimpleSelect } from '../shared/simple-select'

const NONE = '__none'

export function ProfilesSection() {
  const { report, ui, setUi, openDialog } = useEditor()
  const selected = getProfile(report, ui.selectedProfile) ?? report.profiles[0]

  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
      <nav aria-label="Profiles" className="flex shrink-0 flex-col gap-1 lg:w-64">
        <ul className="flex flex-col gap-1">
          {report.profiles.map((p) => (
            <ProfileNavItem key={p.key} profile={p} active={p.key === selected?.key} onSelect={() => setUi({ selectedProfile: p.key })} />
          ))}
        </ul>
        <Button variant="ghost" size="sm" className="justify-start text-muted-foreground" onClick={() => openDialog({ type: 'newProfile' })}>
          <PlusIcon /> New profile
        </Button>
      </nav>

      <div className="min-w-0 flex-1">
        {selected ? (
          <ProfileDetail key={selected.key} profile={selected} />
        ) : (
          <div className="flex flex-col items-start gap-3 rounded-lg border border-dashed p-6">
            <p className="text-muted-foreground">This report has no profiles yet.</p>
            <Button size="sm" onClick={() => openDialog({ type: 'newProfile' })}>
              <PlusIcon /> New profile
            </Button>
          </div>
        )}
      </div>
    </div>
  )
}

function issueCount(report: ReturnType<typeof useEditor>['report'], key: string) {
  return profileGaps(report, key).length + unresolvedVariables(report, key).length
}

function ProfileNavItem({ profile, active, onSelect }: { profile: Profile; active: boolean; onSelect: () => void }) {
  const { report, ui } = useEditor()
  const isDefault = report.settings.defaultProfile === profile.key
  const issues = ui.advanced ? issueCount(report, profile.key) : 0
  const status = isDefault ? 'Default' : profile.enabled ? 'Available' : 'Hidden'

  return (
    <li>
      <button
        type="button"
        onClick={onSelect}
        aria-current={active ? 'true' : undefined}
        className={cn(
          'flex w-full items-start gap-2.5 rounded-md px-2.5 py-2 text-left outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring/60',
          active ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:bg-accent/50 hover:text-foreground',
        )}
      >
        <CircleUserIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span className="flex min-w-0 flex-1 flex-col">
          <span className={cn('truncate font-medium', active && 'text-foreground')}>{profile.name}</span>
          <span className="truncate text-xs text-muted-foreground">{status}</span>
        </span>
        {issues > 0 && (
          <span className={cn('mt-0.5 inline-flex shrink-0 items-center gap-1 rounded px-1.5 text-xs tabular-nums', WARN_CHIP)}>
            <AlertTriangleIcon className="size-3" aria-hidden />
            {issues}
            <span className="sr-only">{issues === 1 ? ' issue' : ' issues'}</span>
          </span>
        )}
      </button>
    </li>
  )
}

function ProfileDetail({ profile }: { profile: Profile }) {
  const { report, ui, mutate, openDialog } = useEditor()
  const isDefault = report.settings.defaultProfile === profile.key
  const mix = profileMix(report, profile)

  return (
    <div className="flex flex-col gap-4">
      <section aria-labelledby={`profile-${profile.key}`} className="flex flex-col gap-3 rounded-lg border bg-card p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 flex-col gap-1">
            <h3 id={`profile-${profile.key}`} className="flex min-w-0 flex-wrap items-center gap-2 font-medium">
              <EditableProfileName profile={profile} />
              {isDefault && <Badge variant="secondary">Default</Badge>}
              {profileScenarios(report, profile).map((sc) => (
                <Badge key={sc.role} variant="outline" className="font-normal" title={`Runs as this profile name one ${sc.name.toLowerCase()}`}>
                  {sc.name}
                </Badge>
              ))}
              {ui.advanced && mix.mixed && (
                <Badge variant="outline" className={cn('font-normal', WARN_CHIP)} title="Some variables read scenario data while others read actuals">
                  Mixed: scenario + actual
                </Badge>
              )}
            </h3>
            <p className="text-xs text-muted-foreground">
              {isDefault
                ? 'The report runs as this profile unless someone picks another.'
                : profile.enabled
                  ? 'Available when running the report.'
                  : 'Hidden when running the report.'}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <label className="mr-2 flex items-center gap-2 text-muted-foreground">
              <Switch
                size="sm"
                checked={profile.enabled}
                disabled={isDefault}
                onCheckedChange={(v) =>
                  mutate((d) => {
                    const p = d.profiles.find((x) => x.key === profile.key)
                    if (p) p.enabled = v
                  })
                }
              />
              Available to run
            </label>
            {!isDefault && (
              <Button variant="outline" size="sm" onClick={() => openDialog({ type: 'defaultProfile', profileKey: profile.key })}>
                <StarIcon data-icon="inline-start" />
                Make default
              </Button>
            )}
            <Button variant="ghost" size="icon-sm" aria-label="Delete profile" onClick={() => openDialog({ type: 'deleteProfile', profileKey: profile.key })}>
              <Trash2Icon />
            </Button>
          </div>
        </div>
      </section>

      <PicksSection profile={profile} />
      {ui.advanced && <DiagnosticsSection profile={profile} />}
    </div>
  )
}

function EditableProfileName({ profile }: { profile: Profile }) {
  const { mutate } = useEditor()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(profile.name)

  const commit = () => {
    const next = draft.trim()
    setEditing(false)
    if (!next || next === profile.name) {
      setDraft(profile.name)
      return
    }
    mutate((d) => {
      const p = d.profiles.find((x) => x.key === profile.key)
      if (p) p.name = next
    })
  }

  if (editing) {
    return (
      <span className="-mx-1.5 inline-grid min-w-56 max-w-full">
        <span aria-hidden="true" className="invisible col-start-1 row-start-1 overflow-hidden whitespace-pre border border-transparent px-1.5 font-medium">
          {draft || ' '}
        </span>
        <Input
          autoFocus
          aria-label="Profile name"
          size={1}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onFocus={(e) => e.currentTarget.select()}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.nativeEvent.isComposing || e.keyCode === 229) return
            if (e.key === 'Enter') {
              e.preventDefault()
              commit()
            } else if (e.key === 'Escape') {
              e.preventDefault()
              setDraft(profile.name)
              setEditing(false)
            }
          }}
          className="col-start-1 row-start-1 h-7 w-full min-w-0 px-1.5 font-medium md:text-sm"
        />
      </span>
    )
  }

  return (
    <button
      type="button"
      onClick={() => {
        setDraft(profile.name)
        setEditing(true)
      }}
      title="Rename profile"
      className="group/name relative isolate flex min-w-0 cursor-text items-center gap-1.5 py-0.5 text-left font-medium outline-none before:absolute before:-inset-x-1.5 before:inset-y-0 before:-z-10 before:rounded-md before:border before:border-dashed before:border-border hover:before:border-ring/60 hover:before:bg-muted focus-visible:before:ring-2 focus-visible:before:ring-ring"
    >
      <span className="sr-only">Rename profile: </span>
      <span className="truncate">{profile.name}</span>
      <PencilIcon className="size-3.5 shrink-0 text-muted-foreground opacity-60 group-hover/name:opacity-100" aria-hidden />
    </button>
  )
}

function PicksSection({ profile }: { profile: Profile }) {
  const { report, ui, setUi, setPick } = useEditor()
  const usage = variableUsage(report)
  const compare = ui.compareProfile && ui.compareProfile !== profile.key ? getProfile(report, ui.compareProfile) : undefined
  const choices = [...usage.keys()].filter((v) => suppliersOf(report, v).length > 1)
  const fixed = [...usage.keys()].filter((v) => suppliersOf(report, v).length <= 1)
  const others = report.profiles.filter((p) => p.key !== profile.key)

  return (
    <section aria-labelledby="picks-heading" className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-col">
          <h4 id="picks-heading" className="font-medium">
            Source picks
          </h4>
          <p className="text-xs text-muted-foreground">Which source this profile reads for variables that more than one source supplies.</p>
        </div>
        {others.length > 0 && choices.length > 0 && (
          <SimpleSelect
            size="sm"
            aria-label="Compare with"
            className="w-52"
            value={compare?.key ?? NONE}
            onChange={(v) => setUi({ compareProfile: v === NONE ? null : v })}
            options={[{ value: NONE, label: 'Compare with…' }, ...others.map((p) => ({ value: p.key, label: p.name }))]}
          />
        )}
      </div>

      {choices.length === 0 ? (
        <p className="text-muted-foreground">Every variable this report reads has only one source, so there is nothing to pick.</p>
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-4">Variable</TableHead>
                <TableHead>Feeds</TableHead>
                <TableHead className="w-64">Read from</TableHead>
                {compare && <TableHead className="w-56">{compare.name}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {choices.map((v) => {
                const picked = pickedSource(report, profile, v)
                const theirs = compare ? pickedSource(report, compare, v) : undefined
                const differs = compare && theirs?.key !== picked?.key
                return (
                  <TableRow key={v}>
                    <TableCell className="pl-4">
                      <KeyText className="text-foreground">{v}</KeyText>
                    </TableCell>
                    <TableCell className="whitespace-normal text-muted-foreground">{usage.get(v)?.join(', ')}</TableCell>
                    <TableCell>
                      <SimpleSelect
                        size="sm"
                        aria-label={`Source for ${v}`}
                        className="w-60"
                        value={picked?.key ?? ''}
                        placeholder="Pick a source"
                        onChange={(s) => setPick(profile.key, v, s)}
                        options={suppliersOf(report, v).map((s) => ({ value: s.key, label: s.name }))}
                      />
                    </TableCell>
                    {compare && (
                      <TableCell className={cn(differs ? 'font-medium' : 'text-muted-foreground')}>
                        {theirs?.name ?? 'Nothing picked'}
                        {differs && <span className="sr-only"> (differs)</span>}
                      </TableCell>
                    )}
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {fixed.length > 0 && (
        <p className="text-xs text-pretty text-muted-foreground">
          Same for every profile (one source each): <span className="font-mono">{fixed.join(', ')}</span>
        </p>
      )}
    </section>
  )
}

function groupByRow(gaps: { rowKey: string; variable: string }[]) {
  const groups = new Map<string, string[]>()
  for (const g of gaps) groups.set(g.rowKey, [...(groups.get(g.rowKey) ?? []), g.variable])
  return [...groups.entries()]
}

function DiagnosticsSection({ profile }: { profile: Profile }) {
  const { report, setUi } = useEditor()
  const gaps = profileGaps(report, profile.key)
  const unresolved = unresolvedVariables(report, profile.key)
  const total = gaps.length + unresolved.length
  const goToRow = (rowKey: string) => setUi({ activeProfile: profile.key, allProfiles: false, view: 'editor', tab: 'rows', selectedRowKey: rowKey })

  return (
    <section aria-labelledby="diagnostics-heading" className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-col">
          <h4 id="diagnostics-heading" className="flex items-center gap-2 font-medium">
            Diagnostics
            {total > 0 && (
              <span className={cn('inline-flex items-center gap-1 rounded px-1.5 text-xs font-normal tabular-nums', WARN_CHIP)}>
                <AlertTriangleIcon className="size-3" aria-hidden />
                {total} {total === 1 ? 'issue' : 'issues'}
              </span>
            )}
          </h4>
          <p className="text-xs text-muted-foreground">Values that would show blank when the report runs as this profile.</p>
        </div>
      </div>

      {total === 0 ? (
        <p className="flex items-center gap-2 text-muted-foreground">
          <CircleCheckIcon className="size-4 text-primary" aria-hidden />
          No issues. Every variable has a source and every picked source is mapped.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {unresolved.length > 0 && (
            <div className={cn('rounded-md border border-dashed px-3 py-2 text-xs text-pretty', WARN_CHIP)}>
              No source picked for <span className="font-mono">{unresolved.join(', ')}</span>. Pick one under Source picks.
            </div>
          )}
          {gaps.length > 0 && (
            <ul className="flex flex-col divide-y rounded-md border">
              {groupByRow(gaps).map(([rowKey, variables]) => (
                <li key={rowKey}>
                  <Collapsible>
                    <div className="flex items-center gap-2 pr-3">
                      <CollapsibleTrigger className="group/row flex min-w-0 flex-1 items-center gap-2 py-2.5 pl-3 text-left outline-none hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring/60">
                        <ChevronRightIcon className="size-4 shrink-0 text-muted-foreground transition-transform group-data-[state=open]/row:rotate-90" aria-hidden />
                        <span className="truncate font-medium">{findRow(report.rows, rowKey)?.label ?? rowKey}</span>
                        <span className={cn('inline-flex shrink-0 items-center rounded px-1.5 text-xs tabular-nums', WARN_CHIP)}>
                          {variables.length} {variables.length === 1 ? 'variable' : 'variables'}
                        </span>
                      </CollapsibleTrigger>
                      <Button variant="outline" size="xs" onClick={() => goToRow(rowKey)}>
                        Open row
                      </Button>
                    </div>
                    <CollapsibleContent>
                      <ul className="flex flex-col gap-1.5 border-t bg-muted/20 py-2.5 pr-3 pl-9">
                        {variables.map((v) => (
                          <li key={v} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
                            <KeyText className="text-foreground">{v}</KeyText>
                            <span className="text-muted-foreground">Picked source isn&apos;t mapped to this row</span>
                          </li>
                        ))}
                      </ul>
                    </CollapsibleContent>
                  </Collapsible>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </section>
  )
}
