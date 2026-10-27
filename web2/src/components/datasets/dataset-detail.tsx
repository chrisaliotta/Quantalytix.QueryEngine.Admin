import { BracesIcon, ClipboardCheckIcon, Columns3Icon, FingerprintIcon, LinkIcon, SettingsIcon, SigmaIcon, type LucideIcon } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { DatasetEditScope, useDataset, useDatasetsApi, useVersion } from '@/lib/datasets/client'
import { useSearchParam } from '@/lib/navigation'
import { useChangeScope, useNavigationGuard } from '@/components/shell/changes'
import type { DatasetTab, DatasetVersion, DatasetValidationReport } from '@/lib/datasets/contract'
import { DetailShell, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { BindingsSection } from './bindings-section'
import { DatasetHeader } from './dataset-header'
import { FieldsSection } from './fields-section'
import { GrainsSection } from './grains-section'
import { MeasuresSection } from './measures-section'
import { ReadinessSection } from './readiness-section'
import { SettingsSection } from './settings-section'
import { ReadOnlyBanner } from './shared'

export type Section = DatasetTab | 'readiness'

export interface SectionProps {
  version: DatasetVersion
  readOnly: boolean
  editingId: string | null
  onEdit: (id: string | null) => void
  onOpen: (tab: DatasetTab, itemId: string | null) => void
  /** The dataset was just created in this editor and has not been saved yet. */
  isNew?: boolean
}

const NAV: { id: Section; label: string; icon: LucideIcon; count?: (v: DatasetVersion) => number }[] = [
  { id: 'fields', label: 'Fields', icon: Columns3Icon, count: (v) => v.fields.length },
  { id: 'grains', label: 'Grains', icon: FingerprintIcon, count: (v) => v.grains.length },
  { id: 'bindings', label: 'Bindings', icon: LinkIcon, count: (v) => v.bindings.length },
  { id: 'measures', label: 'Measures', icon: SigmaIcon, count: (v) => v.measures.length },
  { id: 'settings', label: 'Settings', icon: SettingsIcon },
]

/** `datasetId: null` opens the editor on a new, unsaved dataset; `onCreated` fires once it is saved. */
export function DatasetDetail({ datasetId, onBack, onCreated }: { datasetId: string | null; onBack: () => void; onCreated?: (id: string) => void }) {
  const { data: dataset } = useDataset(datasetId)
  return (
    <DatasetEditScope datasetId={datasetId ?? 'new'} label={dataset?.displayName ?? 'New dataset'}>
      {datasetId ? <DatasetDetailBody datasetId={datasetId} onBack={onBack} /> : <NewDataset onBack={onBack} onCreated={onCreated ?? (() => {})} />}
    </DatasetEditScope>
  )
}

/**
 * Stages an empty managed table + draft so the regular editor can fill it in. Nothing persists until Save;
 * Discard, Undo or leaving drops it, which sends the user back to the list.
 */
function NewDataset({ onBack, onCreated }: { onBack: () => void; onCreated: (id: string) => void }) {
  const api = useDatasetsApi()
  const scope = useChangeScope()
  const started = useRef(false)
  const wasDirty = useRef(false)
  const [id, setId] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const count = scope?.count ?? 0

  useEffect(() => {
    if (started.current) return
    started.current = true
    void (async () => {
      const [list, sources] = await Promise.all([api.listDatasets(), api.listSourceObjects()])
      const taken = new Set<string>([...list.map((d) => d.key), ...sources.map((s) => s.name)])
      let n = 1
      const key = () => (n === 1 ? 'untitled' : `untitled_${n}`)
      while (taken.has(key())) n++
      const r = await api.createDataset({ mode: 'new', key: key(), displayName: n === 1 ? 'Untitled dataset' : `Untitled dataset ${n}`, tableName: key(), columns: [] })
      if (r.ok) setId(r.data.id)
      else setFailure(r.problem.title)
    })()
  }, [api])

  useEffect(() => {
    if (!id) return
    if (count > 0) {
      wasDirty.current = true
      return
    }
    if (!wasDirty.current) return
    void api.getDataset(id).then((r) => (r.ok ? onCreated(id) : onBack()))
  }, [id, count, api, onCreated, onBack])

  if (failure) {
    return (
      <PageMessage
        action={
          <Button size="sm" variant="outline" onClick={onBack}>
            Back to datasets
          </Button>
        }
      >
        {failure}
      </PageMessage>
    )
  }
  if (!id) return <PageMessage>Preparing new dataset…</PageMessage>
  return <DatasetDetailBody datasetId={id} onBack={onBack} isNew />
}

function DatasetDetailBody({ datasetId, onBack, isNew }: { datasetId: string; onBack: () => void; isNew?: boolean }) {
  const guard = useNavigationGuard()
  const { data: dataset, error } = useDataset(datasetId)
  const [versionParam, setVersionParam] = useSearchParam('version')
  const [section, setSection] = useState<Section>('fields')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [reports, setReports] = useState<Record<string, DatasetValidationReport>>({})

  const versions = dataset?.versions ?? []
  const selected =
    versions.find((v) => v.id === versionParam) ??
    versions.find((v) => v.status === 'draft') ??
    versions.find((v) => v.id === dataset?.activeVersionId) ??
    versions[0]
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

  const readOnly = selected.status !== 'draft'
  const report = reports[selected.id] ?? null
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
          label: 'Readiness',
          icon: report?.violations.length ? BracesIcon : ClipboardCheckIcon,
          badge: report ? (report.violations.length ? report.violations.length : 'OK') : undefined,
          tone: report ? (report.violations.length ? 'bad' : 'good') : undefined,
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
          onReport={(r) => {
            setReports((all) => ({ ...all, [r.versionId]: r }))
            if (r.violations.length) go('readiness')
          }}
        />
      }
      nav={<SectionNav label="Dataset sections" groups={groups} active={section} onSelect={(s) => go(s)} />}
    >
      {readOnly && section !== 'readiness' && <ReadOnlyBanner status={selected.status} versionNumber={selected.versionNumber} />}
      {!version ? (
        <p className="text-sm text-muted-foreground">Loading version…</p>
      ) : section === 'readiness' ? (
        <ReadinessSection version={version} report={report} onReport={(r) => setReports((all) => ({ ...all, [r.versionId]: r }))} onOpen={go} />
      ) : (
        <SectionBody section={section} props={{ version, readOnly, editingId, onEdit: setEditingId, onOpen: go, isNew }} />
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
