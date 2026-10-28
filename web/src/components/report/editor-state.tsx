import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { useModelDocument } from '@/lib/models/client'
import type { ModelDocument } from '@/lib/models/contract'
import { useReportDiagnostics, useReportDocument, useReportEdit, type RunReportEdit } from '@/lib/reports/client'
import type { Id, ReportDiagnostics, ReportDocument } from '@/lib/reports/contract'
import { useTenantCatalog } from '@/lib/tenant/client'
import type { TenantCatalog } from '@/lib/tenant/contract'

export type EditorTab = 'rows' | 'data' | 'columns' | 'styles'
export type SettingsSection = 'defaults' | 'profiles' | 'dimensions' | 'scenarios' | 'scopes' | 'diagnostics'

export type EditorDialog = { type: 'coverage'; sourceKey: string } | { type: 'copy'; changeModel: boolean } | { type: 'delete' } | null

export interface EditorUi {
  view: 'editor' | 'settings'
  tab: EditorTab
  section: SettingsSection
  selectedRowKey: string | null
  collapsed: string[]
  /** The profile whose resolution the rows show; null follows the report's default. */
  profileKey: string | null
  dialog: EditorDialog
}

interface EditorValue {
  reportId: Id
  /** The report as stored, or as the staged edits leave it. */
  report: ReportDocument
  /** The server's diagnostics of the same state (OI-05). */
  diagnostics: ReportDiagnostics | undefined
  /** The report's model, whose metrics and parameters the editor offers. */
  model: ModelDocument | undefined
  /** Names of datasets, roles and dimensions, which the report names by id. */
  catalog: TenantCatalog | undefined
  ui: EditorUi
  setUi: (patch: Partial<EditorUi>) => void
  /** Runs an edit against the staged port; answers the server's refusal, or null. */
  run: RunReportEdit
}

const EditorContext = createContext<EditorValue | null>(null)

/** Loads the open report with what the editor reads around it, and holds the editor's view state. */
export function ReportEditorProvider({ reportId, children, loading }: { reportId: Id; children: ReactNode; loading: ReactNode }) {
  const { data: report } = useReportDocument(reportId)
  const { data: diagnostics } = useReportDiagnostics(reportId)
  const { data: model } = useModelDocument(report?.modelId ?? null)
  const { data: catalog } = useTenantCatalog()
  const run = useReportEdit()
  const [ui, setUiState] = useState<EditorUi>({
    view: 'editor',
    tab: 'rows',
    section: 'defaults',
    selectedRowKey: null,
    collapsed: [],
    profileKey: null,
    dialog: null,
  })
  const setUi = useCallback((patch: Partial<EditorUi>) => setUiState((prev) => ({ ...prev, ...patch })), [])

  if (!report) return loading
  return <EditorContext.Provider value={{ reportId, report, diagnostics, model, catalog, ui, setUi, run }}>{children}</EditorContext.Provider>
}

export function useReportEditor() {
  const value = useContext(EditorContext)
  if (!value) throw new Error('useReportEditor must be used inside ReportEditorProvider')
  return value
}

/** Lookups by id into the tenant catalog and the model, for labels. */
export function useNames() {
  const { catalog, model } = useReportEditor()
  return {
    role: (id: Id | null) => catalog?.roles.find((r) => r.id === id),
    dataset: (id: Id) => catalog?.datasets.find((d) => d.id === id),
    dimension: (id: Id) => catalog?.dimensions.find((d) => d.id === id),
    variable: (key: string) => model?.variables.find((v) => v.key === key),
    measure: (key: string) => model?.measures.find((m) => m.key === key),
  }
}
