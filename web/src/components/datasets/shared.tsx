import { ArrowLeftIcon, LockIcon, OctagonXIcon, PlusIcon, XIcon } from 'lucide-react'
import { useChangeScope } from '@/components/shell/changes'
import { useCallback, useId, useState, type MouseEvent, type ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useRefreshDatasets } from '@/lib/datasets/client'
import type { VocabularyTerm } from '@/lib/api/contract'
import type { ApiResult, DatasetTab, DatasetDependent, Problem, VersionStatus, Violation } from '@/lib/datasets/contract'
import { cn } from '@/lib/utils'

export function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return <th className={cn('h-9 px-3 text-left text-xs font-medium whitespace-nowrap text-muted-foreground', className)}>{children}</th>
}
export function Td({ children, className, colSpan, onClick }: { children?: ReactNode; className?: string; colSpan?: number; onClick?: (e: MouseEvent<HTMLTableCellElement>) => void }) {
  return (
    <td colSpan={colSpan} onClick={onClick} className={cn('px-3 py-2 align-middle', className)}>
      {children}
    </td>
  )
}

/**
 * For inline add rows: when every error is about an input the row itself has, return them for the row to show.
 * Otherwise return null so the caller opens the item in its properties panel to finish it there.
 */
export function rowErrorsOr(problem: Problem, rowPaths: string[]): Record<string, string[]> | null {
  const paths = Object.keys(problem.errors)
  if (!paths.length) return { '': [problem.detail ?? problem.title] }
  return paths.every((p) => rowPaths.includes(p)) ? problem.errors : null
}

/**
 * Section header control for row-based tabs. Idle: "Add new row". With a pending row or unsaved edits it becomes
 * Cancel + Save: Save adds the pending row (if any) and saves the batch; Cancel drops the pending row, or discards
 * the staged edits when there is none.
 */
export function RowEditActions({
  adding,
  creating,
  canCreate = true,
  onStartNew,
  onCreate,
  onCancelNew,
}: {
  adding: boolean
  creating: boolean
  canCreate?: boolean
  onStartNew: () => void
  onCreate: () => Promise<boolean>
  onCancelNew: () => void
}) {
  const scope = useChangeScope()
  const dirty = (scope?.count ?? 0) > 0
  const busy = creating || !!scope?.saving
  if (!adding && !dirty) {
    return (
      <Button variant="outline" onClick={onStartNew}>
        <PlusIcon /> Add new row
      </Button>
    )
  }
  async function save() {
    if (adding && !(await onCreate())) return
    await scope?.save()
  }
  return (
    <div className="flex items-center gap-2">
      <Button variant="ghost" disabled={busy} onClick={() => (adding ? onCancelNew() : void scope?.discard())}>
        Cancel
      </Button>
      <Button disabled={busy || (adding && !canCreate)} onClick={save}>
        {busy ? 'Saving…' : 'Save'}
      </Button>
    </div>
  )
}

export function SectionHeader({ title, help, action }: { title: string; help: string; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-3">
      <div className="flex flex-col gap-1">
        <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
        <p className="text-sm text-muted-foreground">{help}</p>
      </div>
      {action}
    </div>
  )
}

export function KeyCell({ k, name }: { k: string; name?: string }) {
  return (
    <span className="flex flex-col">
      <span className="font-mono text-xs">{k}</span>
      {name && <span className="text-xs text-muted-foreground">{name}</span>}
    </span>
  )
}

export function Chip({ children, tone = 'default', className, title }: { children: ReactNode; tone?: 'default' | 'warn' | 'muted' | 'primary'; className?: string; title?: string }) {
  return (
    <span
      title={title}
      className={cn(
        'inline-flex items-center gap-1 rounded-sm border px-1.5 py-0.5 font-mono text-[11px] leading-none whitespace-nowrap',
        tone === 'default' && 'bg-muted text-foreground',
        tone === 'muted' && 'text-muted-foreground',
        tone === 'warn' && 'border-warning/50 bg-warning-muted text-warning-foreground',
        tone === 'primary' && 'border-primary/40 bg-primary/10 text-primary',
        className,
      )}
    >
      {children}
    </span>
  )
}

export const STATUS_TONE: Record<VersionStatus, string> = {
  draft: 'border-warning/50 bg-warning-muted text-warning-foreground',
  validated: 'border-primary/40 bg-primary/10 text-primary',
  published: 'border-transparent bg-primary text-primary-foreground',
  retired: 'text-muted-foreground',
}

export function StatusBadge({ status, label }: { status: VersionStatus; label?: string }) {
  return (
    <Badge variant="outline" className={cn('capitalize', STATUS_TONE[status])}>
      {label ?? status}
    </Badge>
  )
}

/** One labelled input with the API's errors for its path shown beneath. */
export function FormRow({
  label,
  htmlFor,
  hint,
  errors,
  children,
  className,
}: {
  label: string
  htmlFor?: string
  hint?: ReactNode
  errors?: string[]
  children: ReactNode
  className?: string
}) {
  const errorId = htmlFor ? `${htmlFor}-error` : undefined
  return (
    <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
      <label htmlFor={htmlFor} className="text-sm font-medium">
        {label}
      </label>
      {children}
      {errors?.length ? (
        <ul id={errorId} className="flex flex-col gap-0.5 text-xs text-destructive">
          {errors.map((e) => (
            <li key={e}>{e}</li>
          ))}
        </ul>
      ) : hint ? (
        <p className="text-xs text-pretty text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  )
}

export function FormSection({ title, description, children, aside }: { title: string; description?: string; children: ReactNode; aside?: ReactNode }) {
  return (
    <section className="flex flex-col gap-4 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-0.5">
          <h3 className="text-sm font-semibold">{title}</h3>
          {description && <p className="text-xs text-pretty text-muted-foreground">{description}</p>}
        </div>
        {aside}
      </div>
      {children}
    </section>
  )
}

export const NONE = '__none__'

export function VocabSelect<V extends string>({
  terms,
  value,
  onChange,
  id,
  disabled,
  invalid,
  noneLabel,
  'aria-label': ariaLabel,
}: {
  terms: VocabularyTerm<V>[] | undefined
  value: V | null
  onChange: (value: V | null) => void
  id?: string
  disabled?: boolean
  invalid?: boolean
  /** Offers an explicit empty choice. */
  noneLabel?: string
  'aria-label'?: string
}) {
  const options = [...(noneLabel ? [{ value: NONE, label: noneLabel }] : []), ...(terms ?? []).map((t) => ({ value: t.value, label: t.label }))]
  return (
    <SimpleSelect
      id={id}
      aria-label={ariaLabel}
      value={value ?? (noneLabel ? NONE : '')}
      onChange={(v) => onChange(v === NONE ? null : (v as V))}
      options={options}
      disabled={disabled}
      className={cn(invalid && 'border-destructive')}
    />
  )
}

export function CheckRow({ checked, onChange, label, description, disabled }: { checked: boolean; onChange: (v: boolean) => void; label: string; description?: string; disabled?: boolean }) {
  const id = useId()
  return (
    <div className="flex items-start gap-2.5">
      <Checkbox id={id} checked={checked} onCheckedChange={(v) => onChange(!!v)} disabled={disabled} className="mt-0.5" />
      <label htmlFor={id} className={cn('flex flex-col gap-0.5 text-sm', disabled && 'text-muted-foreground')}>
        <span className="font-medium">{label}</span>
        {description && <span className="text-xs text-muted-foreground">{description}</span>}
      </label>
    </div>
  )
}

export const TAB_LABEL: Record<DatasetTab, string> = {
  fields: 'Fields',
  grains: 'Grains',
  bindings: 'Bindings',
  measures: 'Measures',
  settings: 'Settings',
}

export function DependentsList({ dependents, onOpen, empty = 'Nothing reads this yet.' }: { dependents: DatasetDependent[]; onOpen?: (d: DatasetDependent) => void; empty?: string }) {
  if (!dependents.length) return <p className="text-sm text-muted-foreground">{empty}</p>
  return (
    <ul className="flex flex-col divide-y rounded-md border">
      {dependents.map((d, i) => (
        <li key={`${d.itemId}-${d.relation}-${i}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
          <span className="flex min-w-0 flex-col">
            {onOpen ? (
              <button type="button" onClick={() => onOpen(d)} className="w-fit truncate text-left font-mono text-xs hover:underline">
                {d.itemKey}
              </button>
            ) : (
              <span className="truncate font-mono text-xs">{d.itemKey}</span>
            )}
            <span className="text-xs text-muted-foreground">
              {TAB_LABEL[d.tab]} · {d.relation}
            </span>
          </span>
          {d.blocksDelete && (
            <span className="shrink-0 text-[11px] text-muted-foreground" title="Keeps the item from being deleted">
              <LockIcon aria-label="Blocks delete" className="size-3.5" />
            </span>
          )}
        </li>
      ))}
    </ul>
  )
}

/** Opens a tab of the dataset editor, optionally on one item. */
export type OpenTab = (tab: DatasetTab, itemId: string | null) => void

function ViolationList({ violations, onOpen }: { violations: Violation[]; onOpen?: OpenTab }) {
  return (
    <ul className="flex flex-col divide-y rounded-md border">
      {violations.map((v, i) => (
        <li key={`${v.code}-${v.target?.itemId}-${i}`} className="flex flex-col px-3 py-2 text-sm">
          {v.target?.itemKey &&
            (onOpen ? (
              <button type="button" onClick={() => onOpen(v.target!.area as DatasetTab, v.target!.itemId ?? null)} className="w-fit truncate text-left font-mono text-xs hover:underline">
                {v.target.itemKey}
              </button>
            ) : (
              <span className="truncate font-mono text-xs">{v.target.itemKey}</span>
            ))}
          <span className="text-xs text-muted-foreground">{v.message}</span>
        </li>
      ))}
    </ul>
  )
}

export function ProblemBanner({ problem, onDismiss, onOpen }: { problem: Problem; onDismiss?: () => void; onOpen?: OpenTab }) {
  const general = Object.entries(problem.errors).filter(([k]) => k === '' || k === 'columns')
  return (
    <div role="alert" className="flex gap-3 rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-sm">
      <OctagonXIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <p className="font-medium text-destructive">{problem.title}</p>
        {problem.detail && <p className="text-pretty">{problem.detail}</p>}
        {general.flatMap(([, msgs]) => msgs).map((m) => (
          <p key={m}>{m}</p>
        ))}
        {!!Object.keys(problem.errors).length && !general.length && <p className="text-muted-foreground">Fix the highlighted inputs and save again.</p>}
        {problem.violations?.length ? <ViolationList violations={problem.violations} onOpen={onOpen} /> : null}
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Dismiss"
          className="inline-flex size-6 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          <XIcon className="size-4" />
        </button>
      )}
    </div>
  )
}

export function ReadOnlyBanner({ status, versionNumber, onClone }: { status: VersionStatus; versionNumber: number; onClone?: () => void }) {
  const why =
    status === 'validated'
      ? 'It passed validation and is waiting to be published. Return it to draft to keep editing.'
      : status === 'published'
        ? 'Published versions are immutable. Clone it into a new draft to make changes.'
        : 'Retired versions are kept for reference only.'
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-muted/50 px-3 py-2 text-sm">
      <LockIcon aria-hidden className="size-4 shrink-0 text-muted-foreground" />
      <p className="min-w-0 flex-1 text-pretty">
        <span className="font-medium">
          Version {versionNumber} is {status} and read-only.
        </span>{' '}
        <span className="text-muted-foreground">{why}</span>
      </p>
      {onClone && status !== 'validated' && (
        <Button size="sm" variant="outline" onClick={onClone}>
          Clone into new draft
        </Button>
      )}
    </div>
  )
}

export function MissingItem({ what, onBack }: { what: string; onBack: () => void }) {
  return (
    <div className="flex flex-col items-start gap-2 text-sm text-muted-foreground">
      <BackLink label="Back" onClick={onBack} />
      <p>This {what} isn&apos;t in the selected version.</p>
    </div>
  )
}

export function BackLink({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <Button variant="ghost" size="sm" className="-ml-2 w-fit text-muted-foreground" onClick={onClick}>
      <ArrowLeftIcon /> {label}
    </Button>
  )
}

/** Pinned to the bottom of an item editor. Hidden on read-only versions so reviewers see the same layout without actions. */
export function EditorFooter({ readOnly, saving, onCancel, onSave, saveLabel, extra }: { readOnly: boolean; saving: boolean; onCancel: () => void; onSave: () => void; saveLabel: string; extra?: ReactNode }) {
  if (readOnly) return null
  return (
    <div className="sticky bottom-0 -mx-4 -mb-4 flex items-center justify-between gap-2 border-t bg-card/95 px-4 py-3 backdrop-blur md:-mx-6 md:-mb-6 md:px-6">
      <div>{extra}</div>
      <div className="flex items-center gap-2">
        <Button variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button onClick={onSave} disabled={saving}>
          {saving ? 'Saving…' : saveLabel}
        </Button>
      </div>
    </div>
  )
}

/** Runs one API write, keeps its problem for the form, and revalidates dataset data on success. */
export function useApiCall() {
  const refresh = useRefreshDatasets()
  const [saving, setSaving] = useState(false)
  const [problem, setProblem] = useState<Problem | null>(null)
  const run = useCallback(
    async <T,>(call: () => Promise<ApiResult<T>>): Promise<T | null> => {
      setSaving(true)
      const result = await call()
      setSaving(false)
      if (!result.ok) {
        setProblem(result.problem)
        return null
      }
      setProblem(null)
      await refresh()
      return result.data
    },
    [refresh],
  )
  /** For writes that succeed with `null` data, such as deletes. */
  const runOk = useCallback(
    async (call: () => Promise<ApiResult<unknown>>): Promise<boolean> => {
      setSaving(true)
      const result = await call()
      setSaving(false)
      if (!result.ok) {
        setProblem(result.problem)
        return false
      }
      setProblem(null)
      await refresh()
      return true
    },
    [refresh],
  )
  const errorsFor = useCallback((path: string) => problem?.errors[path], [problem])
  return { run, runOk, saving, problem, setProblem, errorsFor }
}

export function DetailLayout({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
      <div className="flex min-w-0 flex-col gap-4">{children}</div>
      {aside && <aside className="flex flex-col gap-4 xl:sticky xl:top-0 xl:self-start">{aside}</aside>}
    </div>
  )
}

export function AsideCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <h3 className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{title}</h3>
      {children}
    </section>
  )
}
