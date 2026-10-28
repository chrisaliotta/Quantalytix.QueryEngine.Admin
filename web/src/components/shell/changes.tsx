import { createContext, useCallback, useContext, useEffect, useEffectEvent, useMemo, useRef, useState, type ReactNode } from 'react'
import { Redo2Icon, Undo2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { humanize, shortcut, useHistory, useHistoryState } from './history'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import type { ChangeSetPort } from '@/lib/api/changes'

/** One editor's pending batch. `count` is 0 when everything is saved. */
export interface ChangeScope {
  id: string
  label: string
  count: number
  saving: boolean
  save: () => Promise<boolean>
  discard: () => Promise<void>
}

interface Registry {
  register: (scope: ChangeScope) => () => void
  guard: (action: () => void) => void
}

const RegistryContext = createContext<Registry | null>(null)
const ScopeContext = createContext<ChangeScope | null>(null)

const plural = (n: number) => `${n} unsaved change${n === 1 ? '' : 's'}`

/** Tracks every open editor's pending batch and stops navigation away from unsaved work. */
export function ChangesProvider({ children }: { children: ReactNode }) {
  const scopes = useRef(new Map<string, ChangeScope>())
  const [pending, setPending] = useState<{ action: () => void; dirty: ChangeScope[] } | null>(null)
  const [busy, setBusy] = useState(false)

  const dirtyScopes = () => [...scopes.current.values()].filter((s) => s.count > 0)

  const register = useCallback((scope: ChangeScope) => {
    scopes.current.set(scope.id, scope)
    return () => {
      if (scopes.current.get(scope.id) === scope) scopes.current.delete(scope.id)
    }
  }, [])

  const guard = useCallback((action: () => void) => {
    const dirty = dirtyScopes()
    if (dirty.length === 0) action()
    else setPending({ action, dirty })
  }, [])

  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (dirtyScopes().length > 0) e.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [])

  const resolve = async (mode: 'save' | 'discard') => {
    if (!pending) return
    setBusy(true)
    let ok = true
    for (const s of pending.dirty) {
      if (mode === 'save') ok = (await s.save()) && ok
      else await s.discard()
    }
    setBusy(false)
    if (!ok) return
    const { action } = pending
    setPending(null)
    action()
  }

  const registry = useMemo(() => ({ register, guard }), [register, guard])
  const total = pending?.dirty.reduce((n, s) => n + s.count, 0) ?? 0

  return (
    <RegistryContext.Provider value={registry}>
      {children}
      <AlertDialog open={!!pending} onOpenChange={(open) => !open && !busy && setPending(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Save changes before leaving?</AlertDialogTitle>
            <AlertDialogDescription>
              {pending && (
                <>
                  {plural(total)} in {pending.dirty.map((s) => s.label).join(', ')}. Edits only apply when they are saved.
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Keep editing</AlertDialogCancel>
            <Button variant="outline" disabled={busy} onClick={() => resolve('discard')}>
              Discard
            </Button>
            <Button disabled={busy} onClick={() => resolve('save')}>
              {busy ? 'Saving…' : 'Save and leave'}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </RegistryContext.Provider>
  )
}

/** Runs `action` at once, or after the user saves or discards any pending batch. */
export function useNavigationGuard() {
  const registry = useContext(RegistryContext)
  return useCallback((action: () => void) => (registry ? registry.guard(action) : action()), [registry])
}

/** Registers a scope and exposes it to the page header's save bar. */
export function ChangeScopeProvider({ scope, children }: { scope: ChangeScope; children: ReactNode }) {
  const registry = useContext(RegistryContext)
  useEffect(() => registry?.register(scope), [registry, scope])
  return <ScopeContext.Provider value={scope}>{children}</ScopeContext.Provider>
}

/** Registers an editor that renders its own save bar, so the navigation guard still covers it. */
export function useRegisterChangeScope(scope: ChangeScope) {
  const registry = useContext(RegistryContext)
  useEffect(() => registry?.register(scope), [registry, scope])
}

export function useChangeScope() {
  return useContext(ScopeContext)
}

/** True while the surrounding editor has unsaved edits; lifecycle actions should be disabled. */
export function useHasPendingChanges() {
  return (useContext(ScopeContext)?.count ?? 0) > 0
}

export const SAVE_FIRST = 'Save or discard your changes first'

type AnyResult = { ok: boolean } | unknown
const failed = (r: AnyResult) => typeof r === 'object' && r !== null && 'ok' in r && (r as { ok: boolean }).ok === false

/**
 * Wraps a module port so edits stage into its change set and lifecycle commands refuse to run over unsaved edits.
 * Screens keep calling the same methods; only Save commits them.
 */
export function useStagedApi<Api extends ChangeSetPort>(
  base: Api,
  opts: { id: string; label: string; edits: readonly (keyof Api)[]; commands: readonly (keyof Api)[]; onSettled: () => unknown },
): { api: Api; scope: ChangeScope } {
  const [count, setCount] = useState(0)
  const [saving, setSaving] = useState(false)
  const onSettled = useRef(opts.onSettled)
  onSettled.current = opts.onSettled
  const settle = () => onSettled.current()
  const edits = useRef(new Set<PropertyKey>(opts.edits))
  const commands = useRef(new Set<PropertyKey>(opts.commands))
  const history = useHistory()
  const scopeId = useRef(opts.id)
  scopeId.current = opts.id

  const api = useMemo(
    () =>
      new Proxy(base, {
        get(target, prop, receiver) {
          const value = Reflect.get(target, prop, receiver)
          if (typeof value !== 'function') return value
          if (edits.current.has(prop)) {
            return async (...args: unknown[]) => {
              const wasOpen = target.hasPendingChanges()
              const before = target.captureState()
              target.beginChanges()
              const result = await value.apply(target, args)
              if (failed(result)) {
                if (!wasOpen) await target.discardChanges()
                return result
              }
              const after = target.captureState()
              setCount((c) => c + 1)
              history.record(
                scopeId.current,
                humanize(String(prop)),
                async () => {
                  target.restoreState(before)
                  if (!wasOpen) await target.discardChanges()
                  setCount((c) => Math.max(0, c - 1))
                  await settle()
                },
                async () => {
                  target.beginChanges()
                  target.restoreState(after)
                  setCount((c) => c + 1)
                  await settle()
                },
              )
              return result
            }
          }
          if (commands.current.has(prop)) {
            return async (...args: unknown[]) => {
              if (target.hasPendingChanges()) {
                return { ok: false, problem: { type: 'about:blank', title: SAVE_FIRST, status: 409, errors: {}, detail: 'This action runs on saved state only.' } }
              }
              return value.apply(target, args)
            }
          }
          return value.bind(target)
        },
      }),
    [base],
  )

  const save = useCallback(async () => {
    if (!base.hasPendingChanges()) {
      setCount(0)
      history.forget(opts.id)
      return true
    }
    setSaving(true)
    const result = await base.commitChanges()
    setSaving(false)
    if (!result.ok) {
      toast.error(result.problem.title, { description: result.problem.detail })
      return false
    }
    setCount((n) => {
      toast.success(n === 1 ? '1 change saved' : `${n} changes saved`)
      return 0
    })
    history.forget(opts.id)
    await settle()
    return true
  }, [base, history, opts.id])

  const discard = useCallback(async () => {
    await base.discardChanges()
    setCount(0)
    history.forget(opts.id)
    await settle()
    toast('Changes discarded')
  }, [base, history, opts.id])

  const mounted = useRef(false)
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      // Deferred so Strict Mode's simulated unmount/remount does not throw away edits staged during mount.
      setTimeout(() => {
        if (mounted.current) return
        history.forget(opts.id)
        if (base.hasPendingChanges()) void base.discardChanges().then(() => settle())
      }, 0)
    }
  }, [base, opts.id, history])

  const scope = useMemo(() => ({ id: opts.id, label: opts.label, count, saving, save, discard }), [opts.id, opts.label, count, saving, save, discard])
  return { api, scope }
}

/** Undo / Redo icon buttons for the shared timeline; hidden until there is something to step through. */
export function HistoryButtons() {
  const history = useHistory()
  const { canUndo, canRedo, undoLabel, redoLabel } = useHistoryState()
  if (!canUndo && !canRedo) return null
  const lower = (s: string | null) => (s ? `: ${s.charAt(0).toLowerCase()}${s.slice(1)}` : '')
  return (
    <div className="flex items-center">
      <Button
        variant="ghost"
        size="icon-sm"
        disabled={!canUndo}
        onClick={() => void history.undo()}
        aria-label={`Undo${lower(undoLabel)}`}
        title={`Undo${lower(undoLabel)} (${shortcut('undo')})`}
      >
        <Undo2Icon />
      </Button>
      <Button
        variant="ghost"
        size="icon-sm"
        disabled={!canRedo}
        onClick={() => void history.redo()}
        aria-label={`Redo${lower(redoLabel)}`}
        title={`Redo${lower(redoLabel)} (${shortcut('redo')})`}
      >
        <Redo2Icon />
      </Button>
    </div>
  )
}

/** Header save bar: amber dot + count, Discard, Save (⌘S). Renders from the nearest scope unless props are passed. */
export function UnsavedChangesBar(props: { count?: number; saving?: boolean; onSave?: () => void; onDiscard?: () => void; idleLabel?: string }) {
  const scope = useChangeScope()
  const count = props.count ?? scope?.count ?? 0
  const saving = props.saving ?? scope?.saving ?? false
  const save = () => {
    if (count === 0 || saving) return
    if (props.onSave) props.onSave()
    else void scope?.save()
  }
  const onSaveShortcut = useEffectEvent(save)
  const doDiscard = () => (props.onDiscard ? props.onDiscard() : void scope?.discard())

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        onSaveShortcut()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="flex items-center gap-2" aria-live="polite">
      <HistoryButtons />
      {count > 0 ? (
        <>
          <span className="flex items-center gap-1.5 text-sm whitespace-nowrap text-muted-foreground">
            <span aria-hidden className="size-2 rounded-full bg-warning" />
            {props.count === undefined && scope ? plural(count) : 'Unsaved changes'}
          </span>
          <Button variant="ghost" size="sm" disabled={saving} onClick={doDiscard}>
            Discard
          </Button>
          <Button size="sm" disabled={saving} onClick={save}>
            {saving ? 'Saving…' : 'Save'}
            <kbd className="ml-1 hidden font-sans text-xs opacity-70 sm:inline">⌘S</kbd>
          </Button>
        </>
      ) : (
        <span className="text-sm whitespace-nowrap text-muted-foreground">{props.idleLabel ?? 'All changes saved'}</span>
      )}
    </div>
  )
}
