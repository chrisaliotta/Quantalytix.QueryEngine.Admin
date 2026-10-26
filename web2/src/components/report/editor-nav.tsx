import {
  BracesIcon,
  ClipboardCheckIcon,
  Columns3Icon,
  DatabaseIcon,
  GitBranchIcon,
  LayersIcon,
  ListTreeIcon,
  type LucideIcon,
  NetworkIcon,
  PaletteIcon,
  SlidersHorizontalIcon,
  UsersIcon,
} from 'lucide-react'
import { SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { allRows } from '@/lib/report/tree'
import { useEditor, type SettingsSectionId, type Tab } from '@/lib/report/store'
import type { ReportState } from '@/lib/report/types'
import { usePublishStatus } from './settings/publish-section'

type NavId = `editor:${Tab}` | `settings:${SettingsSectionId}`

interface Item {
  id: NavId
  label: string
  icon: LucideIcon
  count?: (r: ReportState) => number
}

const GROUPS: { label: string; items: Item[] }[] = [
  {
    label: 'Build',
    items: [
      { id: 'editor:rows', label: 'Rows', icon: ListTreeIcon, count: (r) => allRows(r.rows).length },
      { id: 'editor:data', label: 'Data', icon: DatabaseIcon, count: (r) => r.sources.length },
      { id: 'editor:columns', label: 'Columns', icon: Columns3Icon, count: (r) => r.columns.length },
    ],
  },
  {
    label: 'Data model',
    items: [
      { id: 'settings:profiles', label: 'Profiles', icon: UsersIcon, count: (r) => r.profiles.length },
      { id: 'settings:breakout', label: 'Dimensions', icon: NetworkIcon, count: (r) => r.breakoutDims.length },
      { id: 'settings:scenarios', label: 'Scenarios', icon: GitBranchIcon, count: (r) => r.reportScenarios.length },
      { id: 'settings:scopes', label: 'Variable scopes', icon: LayersIcon, count: (r) => r.rowScopes.length },
    ],
  },
  {
    label: 'Report',
    items: [
      { id: 'settings:defaults', label: 'Defaults', icon: SlidersHorizontalIcon },
      { id: 'editor:styles', label: 'Styles', icon: PaletteIcon, count: (r) => r.styles.length },
    ],
  },
]

export function EditorNav() {
  const { report, ui, setUi } = useEditor()
  const publish = usePublishStatus()

  const active: NavId = ui.view === 'settings' ? `settings:${ui.settingsSection}` : `editor:${ui.tab}`
  const blocking = publish.ready.issues.length

  const groups: NavGroup<NavId>[] = [
    ...GROUPS.map((g) => ({
      label: g.label,
      items: g.items.map(({ id, label, icon, count }) => ({ id, label, icon, badge: count ? count(report) : undefined })),
    })),
    {
      label: 'Release',
      items: [
        {
          id: 'settings:publish',
          label: 'Diagnostics',
          icon: publish.overall === 'fail' ? BracesIcon : ClipboardCheckIcon,
          badge: publish.overall === 'pass' ? 'OK' : publish.overall === 'warn' ? 'Warn' : blocking,
          tone: publish.overall === 'pass' ? 'good' : publish.overall === 'fail' ? 'bad' : undefined,
        },
      ],
    },
  ]

  const go = (id: NavId) => {
    const [kind, key] = id.split(':') as ['editor' | 'settings', string]
    setUi(kind === 'editor' ? { view: 'editor', tab: key as Tab } : { view: 'settings', settingsSection: key as SettingsSectionId })
  }

  return <SectionNav label="Report sections" groups={groups} active={active} onSelect={go} />
}
