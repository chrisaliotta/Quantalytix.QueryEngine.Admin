import { ArrowRightLeftIcon, CircleCheckIcon, InfoIcon, LayersIcon, LinkIcon, OctagonXIcon, RulerIcon, SlidersHorizontalIcon, SplitIcon, TagIcon, UsersIcon, XIcon, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { DetailShell, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { BridgesSection } from '@/components/mappings/bridges-section'
import { MappingSetsSection } from '@/components/mappings/mapping-sets-section'
import { DimensionEditScope, useDimension, useDimensionVersion } from '@/lib/dimensions/client'
import { useSearchParam, useSetSearchParams } from '@/lib/navigation'
import { useNavigationGuard } from '@/components/shell/changes'
import { defaultVersion, isVersionEditable, SELF_VERSIONED_TABS, tabsForKind, type DimensionDetail as Detail, type DimensionTab, type EditPolicy, type ValidationReport, type VersionContent } from '@/lib/dimensions/contract'
import { useBridges, useMappingSets } from '@/lib/mappings/client'
import { cn } from '@/lib/utils'
import { BandsSection } from './bands-section'
import { DimensionHeader } from './dimension-header'
import { LevelsSection } from './levels-section'
import { MembersSection } from './members-section'
import { RolesSection } from './roles-section'
import { PropertiesSection, UsageSection } from './usage-settings-sections'

export interface SectionProps {
  dim: Detail
  content: VersionContent
  editable: boolean
  policy: EditPolicy | null
  /** An item to open, set by a validation link. Sections consume and clear it. */
  focusId: string | null
  clearFocus: () => void
}

const TAB_META: Record<DimensionTab, { label: string; icon: LucideIcon }> = {
  properties: { label: 'Properties', icon: SlidersHorizontalIcon },
  members: { label: 'Members', icon: UsersIcon },
  levels: { label: 'Levels', icon: LayersIcon },
  bands: { label: 'Range bands', icon: RulerIcon },
  roles: { label: 'Roles', icon: TagIcon },
  mappings: { label: 'Mappings', icon: ArrowRightLeftIcon },
  bridges: { label: 'Bridges', icon: SplitIcon },
  usage: { label: 'Usage', icon: LinkIcon },
}

export function DimensionDetail({ dimensionId, onBack }: { dimensionId: string; onBack: () => void }) {
  const { data: dim, error } = useDimension(dimensionId)
  const [versionParam, setVersionParam] = useSearchParam('version')
  const [tabParam] = useSearchParam('tab')
  const setSearchParams = useSetSearchParams()
  const [reports, setReports] = useState<Record<string, ValidationReport>>({})
  const [focusId, setFocusId] = useState<string | null>(null)
  const guard = useNavigationGuard()

  const selected = dim ? (dim.versions.find((v) => v.id === versionParam) ?? defaultVersion(dim.versions)) : undefined
  const { data: content } = useDimensionVersion(selected?.id ?? null)
  const { data: mappingSets } = useMappingSets(dimensionId)
  const { data: bridges } = useBridges(dimensionId)

  if (error) {
    return (
      <PageMessage
        action={
          <Button variant="outline" size="sm" onClick={onBack}>
            Back to dimensions
          </Button>
        }
      >
        This dimension doesn&apos;t exist or isn&apos;t available here.
      </PageMessage>
    )
  }
  if (!dim || !selected) return <PageMessage>Loading dimension…</PageMessage>

  const tabs = tabsForKind(dim.kind, dim.purpose)
  const tab: DimensionTab = tabs.includes(tabParam as DimensionTab) ? (tabParam as DimensionTab) : 'properties'
  const selfVersioned = SELF_VERSIONED_TABS.includes(tab)
  const policy = dim.profile?.editPolicy ?? null
  const editable = isVersionEditable(selected.status, policy)
  const report = reports[selected.id] ?? null
  const contentReady = content && content.version.id === selected.id ? content : null

  const go = (t: DimensionTab, itemId: string | null = null) => {
    setSearchParams({ tab: t === 'properties' ? null : t, ms: null, msv: null, br: null, brv: null })
    setFocusId(itemId)
  }

  const countFor = (t: DimensionTab): number | undefined => {
    if (t === 'mappings') return mappingSets?.length
    if (t === 'bridges') return bridges?.length
    if (t === 'roles') return dim.roles.length
    if (t === 'usage') return dim.usage.length
    if (!contentReady) return undefined
    if (t === 'members') return contentReady.members.length
    if (t === 'levels') return contentReady.levels.length
    if (t === 'bands') return contentReady.bands.length
    return undefined
  }
  const groups: NavGroup<DimensionTab>[] = [
    {
      items: tabs.map((t) => {
        const issues = report?.violations.filter((v) => v.tab === t).length ?? 0
        return { id: t, ...TAB_META[t], badge: issues > 0 ? issues : countFor(t), tone: issues > 0 ? ('bad' as const) : undefined }
      }),
    },
  ]

  return (
    <DimensionEditScope dimensionId={dim.id} label={dim.displayName}>
    <DetailShell
      header={
        <DimensionHeader
          dim={dim}
          selected={selected}
          onBack={onBack}
          onSelectVersion={(id) =>
            guard(() => {
              setVersionParam(id)
              setFocusId(null)
            })
          }
          onReport={(r) => setReports((all) => ({ ...all, [r.versionId]: r }))}
        />
      }
      nav={<SectionNav label="Dimension sections" groups={groups} active={tab} onSelect={(t) => go(t)} />}
    >
            {dim.profile?.visibility === 'module' && (
              <div className="flex items-center gap-2 rounded-md border border-primary/40 bg-primary/10 px-3 py-2 text-sm">
                <InfoIcon aria-hidden className="size-4 shrink-0 text-primary" />
                Managed from the {dim.profile.owner} module.
              </div>
            )}
            {!selfVersioned && report && <ValidationPanel report={report} onOpen={go} onDismiss={() => setReports(({ [selected.id]: _, ...rest }) => rest)} />}
            {!selfVersioned && !editable && <ReadOnlyNote status={selected.status} locked={policy === 'locked'} owner={dim.profile?.owner} versionNumber={selected.versionNumber} />}
            {tab === 'mappings' ? (
              <MappingSetsSection dim={dim} />
            ) : tab === 'bridges' ? (
              <BridgesSection dim={dim} />
            ) : !contentReady ? (
              <p className="text-sm text-muted-foreground">Loading version…</p>
            ) : (
              <TabBody tab={tab} props={{ dim, content: contentReady, editable, policy, focusId, clearFocus: () => setFocusId(null) }} />
            )}
    </DetailShell>
    </DimensionEditScope>
  )
}

function TabBody({ tab, props }: { tab: DimensionTab; props: SectionProps }) {
  switch (tab) {
    case 'members':
      return <MembersSection {...props} />
    case 'levels':
      return <LevelsSection {...props} />
    case 'bands':
      return <BandsSection {...props} />
    case 'roles':
      return <RolesSection {...props} />
    case 'usage':
      return <UsageSection {...props} />
    case 'properties':
      return <PropertiesSection {...props} />
    case 'mappings':
    case 'bridges':
      return null
  }
}

function ReadOnlyNote({ status, locked, owner, versionNumber }: { status: string; locked: boolean; owner?: string; versionNumber: number }) {
  return (
    <p className="flex items-center gap-2 text-sm text-muted-foreground">
      <InfoIcon aria-hidden className="size-4 shrink-0" />
      {locked ? `Read-only. Managed by ${owner}.` : `v${versionNumber} is a frozen snapshot and read-only. Edit the live version instead.`}
    </p>
  )
}

function ValidationPanel({ report, onOpen, onDismiss }: { report: ValidationReport; onOpen: (tab: DimensionTab, id: string | null) => void; onDismiss: () => void }) {
  const ok = !report.violations.length
  return (
    <section
      aria-label="Validation results"
      className={cn('flex flex-col gap-2 rounded-lg border p-3', ok ? 'border-success/40 bg-success-muted' : 'border-destructive/50 bg-destructive/10')}
    >
      <div className="flex items-center gap-2">
        {ok ? <CircleCheckIcon aria-hidden className="size-4 text-success-foreground" /> : <OctagonXIcon aria-hidden className="size-4 text-destructive" />}
        <h2 className="flex-1 text-sm font-medium">{ok ? 'No violations — ready to publish.' : `${report.violations.length} violation${report.violations.length === 1 ? '' : 's'} block publishing`}</h2>
        <Button variant="ghost" size="icon-sm" aria-label="Dismiss results" onClick={onDismiss}>
          <XIcon />
        </Button>
      </div>
      {!ok && (
        <ul className="flex flex-col divide-y divide-destructive/20">
          {report.violations.map((v, i) => (
            <li key={`${v.code}-${i}`} className="flex items-center justify-between gap-3 py-1.5 text-sm">
              <span className="text-pretty">{v.message}</span>
              <Button variant="link" size="sm" className="h-auto shrink-0 p-0" onClick={() => onOpen(v.tab, v.itemId ?? null)}>
                Go to {TAB_META[v.tab].label.toLowerCase()}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
