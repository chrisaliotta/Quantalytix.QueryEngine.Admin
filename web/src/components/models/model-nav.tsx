import { BracesIcon, FunctionSquareIcon, GitBranchIcon, SettingsIcon, SlidersHorizontalIcon, type LucideIcon } from 'lucide-react'
import { SectionNav, type NavGroup } from '@/components/shell/page-shell'
import type { CalcModel } from '@/lib/model/types'

const SECTION_ICON: Record<ModelSection, LucideIcon> = {
  variables: BracesIcon,
  measures: FunctionSquareIcon,
  parameters: SlidersHorizontalIcon,
  attribution: GitBranchIcon,
  general: SettingsIcon,
}

export type ModelSection = 'variables' | 'measures' | 'parameters' | 'attribution' | 'general'

export const MODEL_SECTION_LABEL: Record<ModelSection, string> = {
  variables: 'Variables',
  measures: 'Calculated measures',
  parameters: 'Parameters',
  attribution: 'Attribution',
  general: 'General',
}

interface NavItem {
  key: ModelSection
  count?: (m: CalcModel) => number
}

const GROUPS: { label: string; items: NavItem[] }[] = [
  {
    label: 'Definitions',
    items: [
      { key: 'variables', count: (m) => m.variables.length },
      { key: 'measures', count: (m) => m.measures.length },
      { key: 'parameters', count: (m) => m.parameters.length },
    ],
  },
  {
    label: 'Analysis',
    items: [{ key: 'attribution', count: (m) => m.variables.filter((v) => v.attribution).length + m.measures.length }],
  },
  {
    label: 'Model',
    items: [{ key: 'general' }],
  },
]

export function ModelNav({ model, active, onSelect }: { model: CalcModel; active: ModelSection; onSelect: (s: ModelSection) => void }) {
  const groups: NavGroup<ModelSection>[] = GROUPS.map((g) => ({
    label: g.label,
    items: g.items.map((item) => ({ id: item.key, label: MODEL_SECTION_LABEL[item.key], icon: SECTION_ICON[item.key], badge: item.count?.(model) })),
  }))
  return <SectionNav label="Model sections" groups={groups} active={active} onSelect={onSelect} />
}
