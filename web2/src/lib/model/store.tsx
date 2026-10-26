import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { useHistory } from '@/components/shell/history'
import { initialModels, reportRefs } from './data'
import { modelViolations } from './rules'
import type { CalcModel, ReportRef, RuleViolation } from './types'

export type EditResult = { ok: true } | { ok: false; violations: RuleViolation[] }

interface ModelsContextValue {
  /** Saved models; reports read these. */
  models: CalcModel[]
  reports: ReportRef[]
  getModel: (key: string) => CalcModel | undefined
  /** The model with unsaved edits applied, or the saved model when there are none. */
  getDraft: (key: string) => CalcModel | undefined
  /** Applies an edit to the unsaved draft, or refuses it when the edited model breaks a rule. */
  edit: (key: string, fn: (draft: CalcModel) => void) => EditResult
  isDirty: (key: string) => boolean
  saveModel: (key: string) => void
  discardModel: (key: string) => void
  /** Puts a draft back, e.g. to undo a discard. */
  restoreDraft: (key: string, draft: CalcModel) => void
  addModel: (model: CalcModel) => void
  removeModel: (key: string) => void
  usedBy: (key: string) => ReportRef[]
}

const ModelsContext = createContext<ModelsContextValue | null>(null)

export function ModelsProvider({ children }: { children: ReactNode }) {
  const [models, setModels] = useState(initialModels)
  const [drafts, setDraftsState] = useState<Record<string, CalcModel>>({})
  const draftsRef = useRef(drafts)
  const setDrafts = useCallback((fn: (all: Record<string, CalcModel>) => Record<string, CalcModel>) => {
    draftsRef.current = fn(draftsRef.current)
    setDraftsState(draftsRef.current)
  }, [])
  const history = useHistory()

  const getModel = useCallback((key: string) => models.find((m) => m.key === key), [models])
  const getDraft = useCallback((key: string) => drafts[key] ?? models.find((m) => m.key === key), [drafts, models])

  /** Puts a draft (or no draft) back for undo/redo. */
  const putDraft = useCallback(
    (key: string, draft: CalcModel | undefined) =>
      setDrafts((all) => {
        if (draft) return { ...all, [key]: draft }
        const { [key]: _, ...rest } = all
        return rest
      }),
    [setDrafts],
  )

  const recordDraft = useCallback(
    (key: string, label: string, before: CalcModel | undefined, after: CalcModel | undefined) => {
      putDraft(key, after)
      history.record(`model:${key}`, label, () => putDraft(key, before), () => putDraft(key, after))
    },
    [putDraft, history],
  )

  const edit = useCallback(
    (key: string, fn: (draft: CalcModel) => void, label = 'Model edit'): EditResult => {
      const existing = draftsRef.current[key]
      const current = existing ?? models.find((m) => m.key === key)
      if (!current) return { ok: false, violations: [{ rule: 'model_exists', message: `No model '${key}'.` }] }
      const next = structuredClone(current)
      fn(next)
      const before = new Set(modelViolations(current).map((v) => v.message))
      const introduced = modelViolations(next).filter((v) => !before.has(v.message))
      if (introduced.length) return { ok: false, violations: introduced }
      if (JSON.stringify(next) === JSON.stringify(current)) return { ok: true }
      recordDraft(key, label, existing, next)
      return { ok: true }
    },
    [models, recordDraft],
  )

  const isDirty = useCallback(
    (key: string) => {
      const draft = drafts[key]
      const saved = models.find((m) => m.key === key)
      return !!draft && JSON.stringify(draft) !== JSON.stringify(saved)
    },
    [drafts, models],
  )

  const dropDraft = useCallback(
    (key: string) => {
      putDraft(key, undefined)
      history.forget(`model:${key}`)
    },
    [putDraft, history],
  )

  const saveModel = useCallback(
    (key: string) => {
      const draft = draftsRef.current[key]
      if (!draft) return
      setModels((all) => all.map((m) => (m.key === key ? draft : m)))
      dropDraft(key)
    },
    [dropDraft],
  )

  const discardModel = useCallback((key: string) => dropDraft(key), [dropDraft])
  const restoreDraft = useCallback(
    (key: string, draft: CalcModel) => recordDraft(key, 'Restore discarded changes', draftsRef.current[key], draft),
    [recordDraft],
  )

  const addModel = useCallback((model: CalcModel) => setModels((all) => [...all, model]), [])
  const removeModel = useCallback(
    (key: string) => {
      setModels((all) => all.filter((m) => m.key !== key))
      dropDraft(key)
    },
    [dropDraft],
  )
  const usedBy = useCallback((key: string) => reportRefs.filter((r) => r.model === key), [])

  const value = useMemo(
    () => ({ models, reports: reportRefs, getModel, getDraft, edit, isDirty, saveModel, discardModel, restoreDraft, addModel, removeModel, usedBy }),
    [models, getModel, getDraft, edit, isDirty, saveModel, discardModel, restoreDraft, addModel, removeModel, usedBy],
  )
  return <ModelsContext.Provider value={value}>{children}</ModelsContext.Provider>
}

export function useModels() {
  const ctx = useContext(ModelsContext)
  if (!ctx) throw new Error('useModels must be used inside ModelsProvider')
  return ctx
}
