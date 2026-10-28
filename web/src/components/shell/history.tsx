import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { toast } from 'sonner'

type Step = () => void | Promise<void>

interface Entry {
  id: number
  scope: string
  label: string
  undo: Step
  redo: Step
}

interface HistoryActions {
  /** Adds an applied edit to the timeline. Ignored while an undo or redo is being applied. */
  record: (scope: string, label: string, undo: Step, redo: Step) => number | null
  undo: () => Promise<void>
  redo: () => Promise<void>
  /** Undoes back through the given edit (and anything made after it). */
  undoThrough: (id: number) => Promise<void>
  /** Drops a scope's steps once its batch is saved or discarded. */
  forget: (scope: string) => void
  latestId: () => number | null
}

interface HistoryState {
  canUndo: boolean
  canRedo: boolean
  undoLabel: string | null
  redoLabel: string | null
}

const ActionsContext = createContext<HistoryActions | null>(null)
const StateContext = createContext<HistoryState>({ canUndo: false, canRedo: false, undoLabel: null, redoLabel: null })

const LIMIT = 200
const TOAST_ID = 'history'

export const isMac = () => typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform || navigator.userAgent)
export const shortcut = (kind: 'undo' | 'redo') => (isMac() ? (kind === 'undo' ? '⌘Z' : '⇧⌘Z') : kind === 'undo' ? 'Ctrl+Z' : 'Ctrl+Y')

const TEXT_INPUTS = new Set(['text', 'search', 'email', 'url', 'tel', 'password', 'number', 'date', 'datetime-local', 'month', 'time', 'week'])

/** Text fields keep the browser's own typing undo. */
function isTextEditing(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false
  if (target.isContentEditable) return true
  if (target instanceof HTMLTextAreaElement) return !target.readOnly
  if (target instanceof HTMLInputElement) return TEXT_INPUTS.has(target.type) && !target.readOnly
  return false
}

const modalOpen = () => !!document.querySelector('[role="dialog"][data-state="open"], [role="alertdialog"][data-state="open"], [role="dialog"][data-open], [role="alertdialog"][data-open]')

/**
 * One undo/redo timeline for the whole workspace. Every staged edit — in any editor — lands here, so ⌘Z / Ctrl+Z,
 * ⇧⌘Z / Ctrl+Y / Ctrl+Shift+Z and toast Undo buttons all walk the same history.
 */
export function HistoryProvider({ children }: { children: ReactNode }) {
  const past = useRef<Entry[]>([])
  const future = useRef<Entry[]>([])
  const nextId = useRef(1)
  const applying = useRef(false)
  const queue = useRef<Promise<void>>(Promise.resolve())
  const [state, setState] = useState<HistoryState>({ canUndo: false, canRedo: false, undoLabel: null, redoLabel: null })

  const sync = useCallback(() => {
    const p = past.current.at(-1)
    const f = future.current.at(-1)
    setState({ canUndo: !!p, canRedo: !!f, undoLabel: p?.label ?? null, redoLabel: f?.label ?? null })
  }, [])

  const enqueue = useCallback((task: () => Promise<void>) => {
    const run = queue.current.then(task, task)
    queue.current = run.catch(() => {})
    return run
  }, [])

  const record = useCallback<HistoryActions['record']>(
    (scope, label, undo, redo) => {
      if (applying.current) return null
      const id = nextId.current++
      past.current.push({ id, scope, label, undo, redo })
      if (past.current.length > LIMIT) past.current.shift()
      future.current = []
      sync()
      return id
    },
    [sync],
  )

  const step = useCallback(
    async (from: typeof past, to: typeof past, kind: 'undo' | 'redo', quiet = false) => {
      const entry = from.current.pop()
      if (!entry) return null
      applying.current = true
      try {
        await entry[kind]()
        to.current.push(entry)
      } catch (err) {
        toast.error(`Could not ${kind} "${entry.label}"`, { id: TOAST_ID, description: err instanceof Error ? err.message : undefined })
        return null
      } finally {
        applying.current = false
        sync()
      }
      if (!quiet) announce(kind, entry.label)
      return entry
    },
    [sync],
  )

  const announce = (kind: 'undo' | 'redo', label: string, count = 1) => {
    const title = `${kind === 'undo' ? 'Undid' : 'Redid'} ${count > 1 ? `${count} changes` : label.charAt(0).toLowerCase() + label.slice(1)}`
    toast(title, {
      id: TOAST_ID,
      description: `${shortcut('undo')} to undo · ${shortcut('redo')} to redo`,
      action:
        kind === 'undo'
          ? { label: 'Redo', onClick: () => void enqueue(async () => void (await step(future, past, 'redo'))) }
          : { label: 'Undo', onClick: () => void enqueue(async () => void (await step(past, future, 'undo'))) },
    })
  }

  const undo = useCallback(
    () =>
      enqueue(async () => {
        if (!past.current.length) return void toast('Nothing to undo', { id: TOAST_ID })
        await step(past, future, 'undo')
      }),
    [enqueue, step],
  )

  const redo = useCallback(
    () =>
      enqueue(async () => {
        if (!future.current.length) return void toast('Nothing to redo', { id: TOAST_ID })
        await step(future, past, 'redo')
      }),
    [enqueue, step],
  )

  const undoThrough = useCallback(
    (id: number) =>
      enqueue(async () => {
        if (!past.current.some((e) => e.id === id)) return
        let count = 0
        let last: Entry | null = null
        while (past.current.length) {
          const entry = await step(past, future, 'undo', true)
          if (!entry) return
          count++
          last = entry
          if (entry.id === id) break
        }
        if (last) announce('undo', last.label, count)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [enqueue, step],
  )

  const forget = useCallback(
    (scope: string) => {
      past.current = past.current.filter((e) => e.scope !== scope)
      future.current = future.current.filter((e) => e.scope !== scope)
      sync()
    },
    [sync],
  )

  const latestId = useCallback(() => past.current.at(-1)?.id ?? null, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!(e.metaKey || e.ctrlKey) || e.altKey || e.repeat) return
      const key = e.key.toLowerCase()
      const wantsUndo = key === 'z' && !e.shiftKey
      const wantsRedo = (key === 'z' && e.shiftKey) || (key === 'y' && !e.shiftKey)
      if (!wantsUndo && !wantsRedo) return
      if (isTextEditing(e.target) || modalOpen()) return
      e.preventDefault()
      void (wantsUndo ? undo() : redo())
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [undo, redo])

  const actions = useMemo(() => ({ record, undo, redo, undoThrough, forget, latestId }), [record, undo, redo, undoThrough, forget, latestId])

  return (
    <ActionsContext.Provider value={actions}>
      <StateContext.Provider value={state}>{children}</StateContext.Provider>
    </ActionsContext.Provider>
  )
}

const noop: HistoryActions = {
  record: () => null,
  undo: async () => {},
  redo: async () => {},
  undoThrough: async () => {},
  forget: () => {},
  latestId: () => null,
}

export function useHistory() {
  return useContext(ActionsContext) ?? noop
}

export function useHistoryState() {
  return useContext(StateContext)
}

/**
 * Toast action bound to the edit that was just made: `toast('Removed x', { action: undoAction() })`.
 * Clicking it undoes through that edit on the shared timeline, so it can be redone afterwards.
 */
export function useUndoAction() {
  const history = useHistory()
  return useCallback(() => {
    const id = history.latestId()
    return { label: 'Undo', onClick: () => (id === null ? undefined : void history.undoThrough(id)) }
  }, [history])
}

/** "updateMemberProperties" → "Update member properties". */
export function humanize(name: string) {
  const words = name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}
