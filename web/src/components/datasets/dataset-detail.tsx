import { BracesIcon, ClipboardCheckIcon, Columns3Icon, FingerprintIcon, LinkIcon, SettingsIcon, SigmaIcon, type LucideIcon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { DatasetEditScope, useDataset, useVersion } from '@/lib/datasets/client'
import { useSearchParam } from '@/lib/navigation'
import { useChangeScope, useNavigationGuard } from '@/components/shell/changes'
import { FrozenVersionNote } from '@/components/snapshots/shared'
import type { DatasetTab, DatasetVersion } from '@/lib/datasets/contract'
import { DetailShell, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { BindingsSection } from './bindings-section'
import { DatasetHeader } from './dataset-header'
import { FieldsSection } from './fields-section'
import { GrainsSection } from './grains-section'
import { MeasuresSection } from './measures-section'
import { ReadinessSection } from './readiness-section'
import { SettingsSection } from './settings-section'

export type Section = DatasetTab | 'readiness'

export interface SectionProps {
  version: DatasetVersion
  readOnly: boolean
  editingId: string | null
  onEdit: (id: string | null) => void
  onOpen: (tab: DatasetTab, itemId: string | null) => void
}

const NAV: { id: Section; label: string; icon: LucideIcon; count?: (v: DatasetVersion) => number }[] = [
  { id: 'fields', label: 'Fields', icon: Columns3Icon, count: (v) => v.fields.length },
  { id: 'grains', label: 'Grains', icon: FingerprintIcon, count: (v) => v.grains.length },
  { id: 'bindings', label: 'Bindings', icon: LinkIcon, count: (v) => v.bindings.length },
  { id: 'measures', label: 'Measures', icon: SigmaIcon, count: (v) => v.measures.length },
  { id: 'settings', label: 'Settings', icon: SettingsIcon },
]

export function DatasetDetail({ datasetId, onBack }: { datasetId: string; onBack: () => void }) {
  const { data: dataset } = useDataset(datasetId)
  return (
    <DatasetEditScope datasetId={datasetId} label={dataset?.displayName ?? 'Dataset'}>
      <DatasetDetailBody datasetId={datasetId} onBack={onBack} />
    </DatasetEditScope>
  )
}

function DatasetDetailBody({ datasetId, onBack }: { datasetId: string; onBack: () => void }) {
  const guard = useNavigationGuard()
  const { data: dataset, error } = useDataset(datasetId)
  const [versionParam, setVersionParam] = useSearchParam('version')
  const [section, setSection] = useState<Section>('fields')
  const [editingId, setEditingId] = useState<string | null>(null)
  const findings = useChangeScope()!.violations

  const versions = dataset?.versions ?? []
  const selected = versions.find((v) => v.id === versionParam) ?? versions.find((v) => v.isLive)
  const { data: version } = useVersion(selected?.id ?? null)

  useEffect(() => {
    if (selected && selected.id !== versionParam) setVersionParam(selected.id)
  }, [selected, versionParam, setVersionParam])

  if (error) {
    return (
      <PageMessage
        action={
          <Button size="sm" variant="outline" onClick={onBack}>
            Back to datasets
          </Button>
        }
      >
        This dataset no longer exists.
      </PageMessage>
    )
  }
  if (!dataset || !selected) return <PageMessage>Loading dataset…</PageMessage>

  const readOnly = !selected.isLive
  const go = (s: Section, itemId: string | null = null) => {
    setSection(s)
    setEditingId(itemId)
  }

  const groups: NavGroup<Section>[] = [
    { items: NAV.map(({ id, label, icon, count }) => ({ id, label, icon, badge: version && count ? count(version) : undefined })) },
    {
      items: [
        {
          id: 'readiness',
          label: 'Rule findings',
          icon: findings.length ? BracesIcon : ClipboardCheckIcon,
          badge: findings.length || undefined,
          tone: findings.length ? 'bad' : undefined,
        },
      ],
    },
  ]

  return (
    <DetailShell
      header={
        <DatasetHeader
          dataset={dataset}
          selected={selected}
          onBack={onBack}
          onSelectVersion={(id) =>
            guard(() => {
              setVersionParam(id)
              setEditingId(null)
            })
          }
        />
      }
      nav={<SectionNav label="Dataset sections" groups={groups} active={section} onSelect={(s) => go(s)} />}
    >
      {readOnly && <FrozenVersionNote version={selected} />}
      {!version ? (
        <p className="text-sm text-muted-foreground">Loading version…</p>
      ) : section === 'readiness' ? (
        <ReadinessSection version={version} findings={findings} onOpen={go} />
      ) : (
        <SectionBody section={section} props={{ version, readOnly, editingId, onEdit: setEditingId, onOpen: go }} />
      )}
    </DetailShell>
  )
}

function SectionBody({ section, props }: { section: DatasetTab; props: SectionProps }) {
  switch (section) {
    case 'fields':
      return <FieldsSection {...props} />
    case 'grains':
      return <GrainsSection {...props} />
    case 'bindings':
      return <BindingsSection {...props} />
    case 'measures':
      return <MeasuresSection {...props} />
    case 'settings':
      return <SettingsSection {...props} />
  }
}
