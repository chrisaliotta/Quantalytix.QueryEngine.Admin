import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useMatch } from 'react-router'
import { useHistory } from '@/components/shell/history'
import { sampleReports } from './mockData'
import type { Mapping, ReportState } from './types'

export type Tab = 'rows' | 'data' | 'columns' | 'styles' | 'preview'
export type SettingsSectionId = 'defaults' | 'profiles' | 'breakout' | 'scenarios' | 'scopes' | 'publish'

export type DialogState =
  | { type: 'mapping'; sourceKey: string; rowKey: string }
  | { type: 'switchPick'; profileKey: string; variable: string; sourceKey: string }
  | { type: 'newProfile' }
  | { type: 'renameProfile'; profileKey: string }
  | { type: 'defaultProfile'; profileKey: string }
  | { type: 'deleteProfile'; profileKey: string }
  | { type: 'coverage'; sourceKey: string }
  | { type: 'deleteRow'; rowKey: string }
  | { type: 'renameReport' }
  | { type: 'renameReportKey' }
  | { type: 'deleteReport' }
  | { type: 'changeModel' }
  | null

export type RowPanelTab = 'mapping' | 'style' | 'parameters' | 'properties' | 'diagnostics'

export interface UiState {
  advanced: boolean
  tab: Tab
  view: 'editor' | 'settings'
  settingsSection: SettingsSectionId
  selectedRowKey: string | null
  rowPanelTab: RowPanelTab
  collapsed: string[]
  /** The profile the editor is viewed "as". */
  activeProfile: string
  /** Advanced: show every source without profile dimming. */
  allProfiles: boolean
  /** Profile checked by diagnostics; null follows the editor's profile. */
  readinessProfile: string | null
  selectedProfile: string
  compareProfile: string | null
  /** The Data tab is showing its inline new-source form. */
  addingSource: boolean
  dialog: DialogState
}

export interface ReportEntry {
  id: string
  report: ReportState
  saved: ReportState
  savedAt: Date | null
}

export interface LibraryItem {
  id: string
  report: ReportState
  dirty: boolean
  savedAt: Date | null
}

interface EditorContextValue {
  library: LibraryItem[]
  activeId: string
  createReport: (from: string, name: string, key: string) => string
  duplicateReport: (id: string) => string
  deleteReport: (id: string) => void
  report: ReportState
  /** Last persisted version of the report. */
  saved: ReportState
  savedAt: Date | null
  dirty: boolean
  save: () => void
  discard: () => void
  ui: UiState
  /** Applies a draft edit and records it for undo/redo; `label` names the step in undo toasts. */
  mutate: (fn: (draft: ReportState) => void, label?: string) => void
  setReport: (next: ReportState, label?: string) => void
  setUi: (patch: Partial<UiState> | ((ui: UiState) => Partial<UiState>)) => void
  openDialog: (dialog: DialogState) => void
  closeDialog: () => void
  setPick: (profileKey: string, variable: string, sourceKey: string) => void
  mapping: {
    add: (sourceKey: string, rowKey: string, memberKey: string, factor?: number) => void
    remove: (sourceKey: string, mappingId: string) => void
    replaceRow: (sourceKey: string, rowKey: string, mappings: Mapping[]) => void
    unmapRow: (sourceKey: string, rowKey: string) => void
    exclude: (sourceKey: string, rowKey: string, memberKey: string) => void
    include: (sourceKey: string, rowKey: string, memberKey: string) => void
    /** Excludes an account from its folder and maps it directly, so it can carry its own factor. */
    breakOut: (sourceKey: string, rowKey: string, memberKey: string) => void
  }
}

const EditorContext = createContext<EditorContextValue | null>(null)

export function uid(prefix = 'id') {
  return `${prefix}_${Math.random().toString(36).slice(2, 9)}`
}

const isDirty = (e: ReportEntry) => e.report !== e.saved && JSON.stringify(e.report) !== JSON.stringify(e.saved)

function uiForReport(report: ReportState): Partial<UiState> {
  return {
    view: 'editor',
    tab: 'rows',
    settingsSection: 'defaults',
    selectedRowKey: report.rows[0]?.key ?? null,
    rowPanelTab: 'mapping',
    collapsed: [],
    activeProfile: report.settings.defaultProfile,
    selectedProfile: report.settings.defaultProfile,
    allProfiles: false,
    readinessProfile: null,
    compareProfile: null,
    addingSource: false,
    dialog: null,
  }
}

const initialReport = sampleReports[0]

export function EditorProvider({ children }: { children: ReactNode }) {
  const [entries, setEntries] = useState<ReportEntry[]>(() =>
    sampleReports.map((r, i) => ({ id: `report_${i + 1}`, report: r, saved: r, savedAt: null })),
  )
  const [activeId, setActiveId] = useState('report_1')
  const active = entries.find((e) => e.id === activeId) ?? entries[0]
  const { report, saved, savedAt } = active
  const dirty = useMemo(() => isDirty(active), [active])

  const history = useHistory()
  const entriesRef = useRef(entries)
  const commitEntries = useCallback((next: ReportEntry[]) => {
    entriesRef.current = next
    setEntries(next)
  }, [])
  const updateEntry = useCallback(
    (id: string, fn: (e: ReportEntry) => ReportEntry) => commitEntries(entriesRef.current.map((e) => (e.id === id ? fn(e) : e))),
    [commitEntries],
  )
  const updateActive = useCallback((fn: (e: ReportEntry) => ReportEntry) => updateEntry(activeId, fn), [activeId, updateEntry])

  /** Every report edit goes through here so it lands on the shared undo/redo timeline. */
  const editReport = useCallback(
    (label: string, produce: (current: ReportState) => ReportState) => {
      const id = activeId
      const entry = entriesRef.current.find((e) => e.id === id)
      if (!entry) return
      const before = entry.report
      const after = produce(before)
      if (after === before || JSON.stringify(after) === JSON.stringify(before)) return
      const put = (report: ReportState) => updateEntry(id, (e) => ({ ...e, report }))
      put(after)
      history.record(`report:${id}`, label, () => put(before), () => put(after))
    },
    [activeId, updateEntry, history],
  )

  const setReport = useCallback((next: ReportState, label = 'Report edit') => editReport(label, () => next), [editReport])

  const [ui, setUiState] = useState<UiState>({
    advanced: false,
    tab: 'rows',
    view: 'editor',
    settingsSection: 'defaults',
    selectedRowKey: 'LOANS',
    rowPanelTab: 'mapping',
    collapsed: [],
    activeProfile: initialReport.settings.defaultProfile,
    allProfiles: false,
    readinessProfile: null,
    selectedProfile: initialReport.settings.defaultProfile,
    compareProfile: null,
    addingSource: false,
    dialog: null,
  })

  // The open report follows the URL; switching reports resets the editor's view state.
  const routedId = useMatch('/reports/:reportId')?.params.reportId
  const routedEntry = routedId && routedId !== activeId ? entries.find((e) => e.id === routedId) : undefined
  if (routedEntry) {
    setActiveId(routedEntry.id)
    setUiState((prev) => ({ ...prev, ...uiForReport(routedEntry.report) }))
  }

  const save = useCallback(() => {
    updateActive((e) => ({ ...e, saved: e.report, savedAt: new Date() }))
    history.forget(`report:${activeId}`)
  }, [updateActive, history, activeId])
  const discard = useCallback(() => {
    updateActive((e) => ({ ...e, report: e.saved }))
    history.forget(`report:${activeId}`)
  }, [updateActive, history, activeId])

  useEffect(() => {
    if (!dirty) return
    const warn = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])

  const mutate = useCallback(
    (fn: (draft: ReportState) => void, label = 'Report edit') =>
      editReport(label, (current) => {
        const draft = structuredClone(current)
        fn(draft)
        return draft
      }),
    [editReport],
  )

  const setUi = useCallback((patch: Partial<UiState> | ((ui: UiState) => Partial<UiState>)) => {
    setUiState((prev) => ({ ...prev, ...(typeof patch === 'function' ? patch(prev) : patch) }))
  }, [])

  const library = useMemo<LibraryItem[]>(
    () => entries.map((e) => ({ id: e.id, report: e.report, dirty: isDirty(e), savedAt: e.savedAt })),
    [entries],
  )

  const addCopy = useCallback(
    (fromId: string, patch: (d: ReportState) => void) => {
      const source = entries.find((e) => e.id === fromId)
      if (!source) return ''
      const copy = structuredClone(source.report)
      patch(copy)
      const id = uid('report')
      commitEntries([...entriesRef.current, { id, report: copy, saved: copy, savedAt: new Date() }])
      return id
    },
    [entries, commitEntries],
  )

  const createReport = useCallback(
    (from: string, name: string, key: string) =>
      addCopy(from, (d) => {
        d.settings.name = name
        d.settings.key = key
        d.enabled = false
      }),
    [addCopy],
  )

  const duplicateReport = useCallback(
    (id: string) => {
      const keys = new Set(entries.map((e) => e.report.settings.key))
      return addCopy(id, (d) => {
        let key = `${d.settings.key}_copy`
        for (let n = 2; keys.has(key); n++) key = `${d.settings.key}_copy${n}`
        d.settings.key = key
        d.settings.name = `${d.settings.name} (copy)`
        d.enabled = false
      })
    },
    [entries, addCopy],
  )

  const deleteReport = useCallback(
    (id: string) => {
      if (entries.length <= 1) return
      const rest = entries.filter((e) => e.id !== id)
      commitEntries(rest)
      history.forget(`report:${id}`)
    },
    [entries, commitEntries, history],
  )

  const openDialog = useCallback((dialog: DialogState) => setUi({ dialog }), [setUi])
  const closeDialog = useCallback(() => setUi({ dialog: null }), [setUi])

  const mapping = useMemo(() => {
    const withSource = (sourceKey: string, fn: (s: ReportState['sources'][number]) => void) =>
      mutate((d) => {
        const source = d.sources.find((s) => s.key === sourceKey)
        if (source) fn(source)
      })
    return {
      add: (sourceKey: string, rowKey: string, memberKey: string, factor = 1) => {
        const id = uid('m')
        withSource(sourceKey, (s) => {
          s.mappings.push({ id, rowKey, memberKey, factor })
          s.leftOut = s.leftOut.filter((k) => k !== memberKey)
        })
      },
      remove: (sourceKey: string, mappingId: string) =>
        withSource(sourceKey, (s) => {
          s.mappings = s.mappings.filter((m) => m.id !== mappingId)
        }),
      replaceRow: (sourceKey: string, rowKey: string, mappings: Mapping[]) =>
        withSource(sourceKey, (s) => {
          s.mappings = [...s.mappings.filter((m) => m.rowKey !== rowKey), ...mappings]
        }),
      unmapRow: (sourceKey: string, rowKey: string) =>
        withSource(sourceKey, (s) => {
          s.mappings = s.mappings.filter((m) => m.rowKey !== rowKey)
        }),
      exclude: (sourceKey: string, rowKey: string, memberKey: string) => {
        const id = uid('m')
        withSource(sourceKey, (s) => {
          s.mappings.push({ id, rowKey, memberKey, factor: -1 })
        })
      },
      include: (sourceKey: string, rowKey: string, memberKey: string) =>
        withSource(sourceKey, (s) => {
          s.mappings = s.mappings.filter((m) => !(m.rowKey === rowKey && m.memberKey === memberKey && m.factor < 0))
        }),
      breakOut: (sourceKey: string, rowKey: string, memberKey: string) => {
        const excludeId = uid('m')
        const directId = uid('m')
        withSource(sourceKey, (s) => {
          const onRow = (m: Mapping) => m.rowKey === rowKey && m.memberKey === memberKey
          if (!s.mappings.some((m) => onRow(m) && m.factor < 0)) s.mappings.push({ id: excludeId, rowKey, memberKey, factor: -1 })
          if (!s.mappings.some((m) => onRow(m) && m.factor > 0)) s.mappings.push({ id: directId, rowKey, memberKey, factor: 1 })
        })
      },
    }
  }, [mutate])

  const setPick = useCallback(
    (profileKey: string, variable: string, sourceKey: string) =>
      mutate((d) => {
        const p = d.profiles.find((x) => x.key === profileKey)
        if (p) p.picks[variable] = sourceKey
      }),
    [mutate],
  )

  const value = useMemo(
    () => ({
      library, activeId, createReport, duplicateReport, deleteReport,
      report, saved, savedAt, dirty, save, discard, ui, mutate, setReport, setUi, openDialog, closeDialog, setPick, mapping,
    }),
    [
      library, activeId, createReport, duplicateReport, deleteReport,
      report, saved, savedAt, dirty, save, discard, ui, mutate, setReport, setUi, openDialog, closeDialog, setPick, mapping,
    ],
  )

  return <EditorContext.Provider value={value}>{children}</EditorContext.Provider>
}

export function useEditor() {
  const ctx = useContext(EditorContext)
  if (!ctx) throw new Error('useEditor must be used inside EditorProvider')
  return ctx
}
