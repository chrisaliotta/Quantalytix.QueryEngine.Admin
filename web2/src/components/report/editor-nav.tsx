import {
  ClipboardCheckIcon,
  Columns3Icon,
  DatabaseIcon,
  GitBranchIcon,
  LayersIcon,
  ListTreeIcon,
  NetworkIcon,
  PaletteIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import { SectionNav, type NavGroup } from '@/components/shell/page-shell'
import type { ReportDocument } from '@/lib/reports/contract'
import { useReportEditor, type EditorTab, type SettingsSection } from './editor-state'

type NavId = `editor:${EditorTab}` | `settings:${SettingsSection}`

interface Item {
  id: NavId
  label: string
  icon: LucideIcon
  count?: (r: ReportDocument) => number
}

const GROUPS: { label: string; items: Item[] }[] = [
  {
    label: 'Build',
    items: [
      { id: 'editor:rows', label: 'Rows', icon: ListTreeIcon, count: (r) => r.rows.length },
      { id: 'editor:data', label: 'Data', icon: DatabaseIcon, count: (r) => r.sources.length },
      { id: 'editor:columns', label: 'Columns', icon: Columns3Icon, count: (r) => r.columns.length },
    ],
  },
  {
    label: 'Data model',
    items: [
      { id: 'settings:profiles', label: 'Profiles', icon: UsersIcon, count: (r) => r.profiles.length },
      { id: 'settings:dimensions', label: 'Dimensions', icon: NetworkIcon, count: (r) => r.calculationDimensions.length },
      { id: 'settings:scenarios', label: 'Scenarios', icon: GitBranchIcon, count: (r) => r.scenarios.length },
      { id: 'settings:scopes', label: 'Variable scopes', icon: LayersIcon, count: (r) => r.variableScopes.length },
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
  const { report, diagnostics, ui, setUi } = useReportEditor()
  const failing = diagnostics?.steps.filter((s) => s.status === 'fail').reduce((n, s) => n + s.violations.length, 0) ?? 0
  const warns = diagnostics?.steps.some((s) => s.status === 'warn') ?? false
  const active: NavId = ui.view === 'settings' ? `settings:${ui.section}` : `editor:${ui.tab}`

  const groups: NavGroup<NavId>[] = [
    ...GROUPS.map((g) => ({
      label: g.label,
      items: g.items.map(({ id, label, icon, count }) => ({ id, label, icon, badge: count ? count(report) : undefined })),
    })),
    {
      label: 'Release',
      items: [
        {
          id: 'settings:diagnostics',
          label: 'Diagnostics',
          icon: ClipboardCheckIcon,
          badge: diagnostics ? (diagnostics.ready ? (warns ? 'Warn' : 'OK') : failing) : undefined,
          tone: diagnostics ? (diagnostics.ready ? 'good' : 'bad') : undefined,
        },
      ],
    },
  ]

  const go = (id: NavId) => {
    const [kind, key] = id.split(':') as ['editor' | 'settings', string]
    setUi(kind === 'editor' ? { view: 'editor', tab: key as EditorTab } : { view: 'settings', section: key as SettingsSection })
  }

  return <SectionNav label="Report sections" groups={groups} active={active} onSelect={go} />
}
