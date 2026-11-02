import { ArrowRightLeftIcon, InfoIcon, LayersIcon, LinkIcon, OctagonXIcon, RulerIcon, SlidersHorizontalIcon, SplitIcon, TagIcon, UsersIcon, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { DetailShell, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { BridgesSection } from '@/components/mappings/bridges-section'
import { MappingSetsSection } from '@/components/mappings/mapping-sets-section'
import { DimensionEditScope, useDimension, useDimensionVersion } from '@/lib/dimensions/client'
import { useSearchParam, useSetSearchParams } from '@/lib/navigation'
import { useChangeScope, useNavigationGuard } from '@/components/shell/changes'
import { FrozenVersionNote } from '@/components/snapshots/shared'
import { isVersionEditable, SELF_VERSIONED_TABS, tabsForKind, type DimensionDetail as Detail, type DimensionTab, type DimensionEditPolicy, type DimensionVersionContent, type DimensionVersionSummary, type Violation } from '@/lib/dimensions/contract'
import { useBridges, useMappingSets } from '@/lib/mappings/client'
import { BandsSection } from './bands-section'
import { DimensionHeader } from './dimension-header'
import { LevelsSection } from './levels-section'
import { MembersSection } from './members-section'
import { RolesSection } from './roles-section'
import { PropertiesSection, UsageSection } from './usage-settings-sections'

export interface SectionProps {
  dim: Detail
  content: DimensionVersionContent
  editable: boolean
  policy: DimensionEditPolicy | null
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
  if (!dim) return <PageMessage>Loading dimension…</PageMessage>
  return (
    <DimensionEditScope dimensionId={dim.id} label={dim.displayName}>
      <DimensionPage dim={dim} onBack={onBack} />
    </DimensionEditScope>
  )
}

function DimensionPage({ dim, onBack }: { dim: Detail; onBack: () => void }) {
  const [versionParam, setVersionParam] = useSearchParam('version')
  const [tabParam] = useSearchParam('tab')
  const setSearchParams = useSetSearchParams()
  const [focusId, setFocusId] = useState<string | null>(null)
  const guard = useNavigationGuard()
  const findings = useChangeScope()!.violations

  const selected = dim.versions.find((v) => v.id === versionParam) ?? dim.versions.find((v) => v.isLive)!
  const { data: content } = useDimensionVersion(selected.id)
  const { data: mappingSets } = useMappingSets(dim.id)
  const { data: bridges } = useBridges(dim.id)

  const tabs = tabsForKind(dim.kind, dim.purpose)
  const tab: DimensionTab = tabs.includes(tabParam as DimensionTab) ? (tabParam as DimensionTab) : 'properties'
  const selfVersioned = SELF_VERSIONED_TABS.includes(tab)
  const policy = dim.profile?.editPolicy ?? null
  const editable = isVersionEditable(selected, policy)
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
        const issues = findings.filter((v) => v.target?.area === t).length
        return { id: t, ...TAB_META[t], badge: issues > 0 ? issues : countFor(t), tone: issues > 0 ? ('bad' as const) : undefined }
      }),
    },
  ]

  return (
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
            {!selfVersioned && findings.length > 0 && <FindingsPanel findings={findings} onOpen={go} />}
            {!selfVersioned && !editable && <ReadOnlyNote version={selected} locked={policy === 'locked'} owner={dim.profile?.owner} />}
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

function ReadOnlyNote({ version, locked, owner }: { version: DimensionVersionSummary; locked: boolean; owner?: string }) {
  if (!locked) return <FrozenVersionNote version={version} />
  return (
    <p className="flex items-center gap-2 text-sm text-muted-foreground">
      <InfoIcon aria-hidden className="size-4 shrink-0" />
      Read-only. Managed by {owner}.
    </p>
  )
}

/** The commit rules' findings on the pending edits; Save stays blocked until none remains. */
function FindingsPanel({ findings, onOpen }: { findings: Violation[]; onOpen: (tab: DimensionTab, id: string | null) => void }) {
  return (
    <section aria-label="Rule findings" className="flex flex-col gap-2 rounded-lg border border-destructive/50 bg-destructive/10 p-3">
      <div className="flex items-center gap-2">
        <OctagonXIcon aria-hidden className="size-4 text-destructive" />
        <h2 className="flex-1 text-sm font-medium">
          {findings.length} rule finding{findings.length === 1 ? '' : 's'} block saving
        </h2>
      </div>
      <ul className="flex flex-col divide-y divide-destructive/20">
        {findings.map((v, i) => (
          <li key={`${v.code}-${i}`} className="flex items-center justify-between gap-3 py-1.5 text-sm">
            <span className="text-pretty">{v.message}</span>
            {v.target && (
              <Button variant="link" size="sm" className="h-auto shrink-0 p-0" onClick={() => onOpen(v.target!.area as DimensionTab, v.target!.itemId ?? null)}>
                Go to {TAB_META[v.target.area as DimensionTab].label.toLowerCase()}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}
