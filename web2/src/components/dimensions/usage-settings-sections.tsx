import { ArrowUpRightIcon, TriangleAlertIcon } from 'lucide-react'
import { useState } from 'react'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useDimensionsApi } from '@/lib/dimensions/client'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/switch'
import { KIND_LABEL, RANGE_TYPE_LABEL, versionLabel, type RangeDataType, type UsageKind, type UsageReference } from '@/lib/dimensions/contract'
import type { SectionProps } from './dimension-detail'
import { ConfirmDialog, EmptyState, Field, Mono, ReadValue, StatusBadge, formatDate, useDimCall } from './shared'

const GROUPS: { title: string; kinds: UsageKind[] }[] = [
  { title: 'Dataset bindings', kinds: ['dataset binding'] },
  { title: 'Mapping sets', kinds: ['mapping set'] },
  { title: 'Bridges', kinds: ['bridge'] },
  { title: 'Reports', kinds: ['calculation dimension', 'report scenario', 'report'] },
  { title: 'Assumption sets', kinds: ['assumption set'] },
]

export function UsageSection({ dim }: SectionProps) {
  if (!dim.usage.length) return <EmptyState title="No references">Nothing references this dimension yet — it can be deleted.</EmptyState>
  return (
    <div className="flex flex-col gap-6">
      {GROUPS.map((g) => {
        const refs = dim.usage.filter((u) => g.kinds.includes(u.kind))
        if (!refs.length) return null
        return (
          <section key={g.title} className="flex flex-col gap-2">
            <h2 className="text-sm font-medium">
              {g.title} <span className="font-normal text-muted-foreground">· {refs.length}</span>
            </h2>
            <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
              {refs.map((u, i) => (
                <UsageCard key={i} usage={u} />
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}

function UsageCard({ usage }: { usage: UsageReference }) {
  return (
    <li>
      <a href={usage.href ?? '#'} className="group flex h-full flex-col gap-1 rounded-lg border bg-card p-3 transition-colors hover:border-foreground/20">
        <span className="flex items-start justify-between gap-2">
          <span className="text-sm font-medium">{usage.name}</span>
          <ArrowUpRightIcon aria-hidden className="size-3.5 shrink-0 text-muted-foreground group-hover:text-foreground" />
        </span>
        <span className="text-xs text-pretty text-muted-foreground">{usage.detail}</span>
        <span className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-xs">
          <span className="text-muted-foreground capitalize">{usage.kind}</span>
          {usage.roleKey && (
            <span>
              role <Mono>{usage.roleKey}</Mono>
            </span>
          )}
          {usage.pinsVersion && <span className="font-mono">→ v{usage.pinsVersion}</span>}
          {usage.memberKeys?.length ? (
            <span className="text-muted-foreground">
              names <Mono>{usage.memberKeys.slice(0, 4).join(', ')}</Mono>
              {usage.memberKeys.length > 4 ? ` +${usage.memberKeys.length - 4}` : ''}
            </span>
          ) : null}
        </span>
      </a>
    </li>
  )
}

export function PropertiesSection({ dim, content, editable }: SectionProps) {
  const api = useDimensionsApi()
  const { run } = useDimCall()
  const v = content.version
  const [pending, setPending] = useState<RangeDataType | null>(null)
  const isRange = dim.kind === 'range'

  const apply = (t: RangeDataType) => run(() => api.updateVersion(v.id, { rangeDataType: t }), `Range data type set to ${RANGE_TYPE_LABEL[t].toLowerCase()}`)

  return (
    <div className="flex max-w-3xl flex-col gap-8">
      <section aria-label="Primary properties" className={`grid gap-6 rounded-lg border bg-card p-5 ${isRange ? 'md:grid-cols-2' : ''}`}>
        <DisplayNameField key={`${dim.id}-${dim.displayName}`} dim={dim} />
        {isRange && (
          <div className="flex flex-col gap-2">
            <label htmlFor="dim-range-type" className="text-sm font-medium">
              Range data type
            </label>
            {editable ? (
              <SimpleSelect
                id="dim-range-type"
                aria-label="Range data type"
                className="h-10 text-base"
                value={v.rangeDataType ?? ''}
                placeholder="Not set"
                onChange={(t) => (content.bands.length ? setPending(t as RangeDataType) : apply(t as RangeDataType))}
                options={(Object.keys(RANGE_TYPE_LABEL) as RangeDataType[]).map((t) => ({ value: t, label: RANGE_TYPE_LABEL[t] }))}
              />
            ) : (
              <p className="flex h-10 items-center text-base">{v.rangeDataType ? RANGE_TYPE_LABEL[v.rangeDataType] : 'Not set'}</p>
            )}
            <p className="text-xs leading-relaxed text-muted-foreground">
              {content.bands.length > 0 && editable ? (
                <span className="flex items-center gap-1.5 text-warning-foreground">
                  <TriangleAlertIcon aria-hidden className="size-3.5 shrink-0" /> Changing the type clears the bands&apos; bounds.
                </span>
              ) : (
                `Bounds of every band use this type. Set per version${editable ? '' : ` — v${v.versionNumber} is frozen`}.`
              )}
            </p>
          </div>
        )}
      </section>

      <DetailsSettings key={`${dim.id}-${dim.isEnabled}-${dim.isRemovable}`} dim={dim} />

      <section className="flex flex-col gap-2">
        <h2 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">This version</h2>
        <div className="grid grid-cols-2 gap-4 rounded-lg border p-4 sm:grid-cols-4">
          <ReadValue label="Version" mono>
            v{v.versionNumber}
          </ReadValue>
          <ReadValue label="Status">
            <StatusBadge status={v.status} label={versionLabel(v.status)} />
          </ReadValue>
          <ReadValue label="Created">{formatDate(v.createdAt)}</ReadValue>
          {v.retiredAt ? <ReadValue label="Frozen">{formatDate(v.retiredAt)}</ReadValue> : <ReadValue label="Live since">{v.publishedAt ? formatDate(v.publishedAt) : null}</ReadValue>}
          {v.freezeNote && (
            <div className="col-span-full">
              <ReadValue label="Freeze note">{v.freezeNote}</ReadValue>
            </div>
          )}
        </div>
      </section>
      <ConfirmDialog
        open={!!pending}
        onOpenChange={(o) => !o && setPending(null)}
        title="Change the range data type?"
        confirmLabel="Change and clear bounds"
        destructive
        onConfirm={async () => {
          if (pending) await apply(pending)
          setPending(null)
        }}
      >
        Changing the type clears the bands&apos; bounds. Members stay assigned to their bands.
      </ConfirmDialog>
    </div>
  )
}

/** Dimension-level, so it stays editable while viewing a frozen version. */
function DisplayNameField({ dim }: { dim: SectionProps['dim'] }) {
  const api = useDimensionsApi()
  const { run, saving, errorsFor } = useDimCall()
  const [name, setName] = useState(dim.displayName)
  const [synced, setSynced] = useState(dim.displayName)
  if (synced !== dim.displayName) {
    setSynced(dim.displayName)
    setName(dim.displayName)
  }
  const locked = dim.profile?.editPolicy === 'locked'
  const stage = () => {
    const next = name.trim()
    if (!next) {
      setName(dim.displayName)
      return
    }
    if (next === dim.displayName || saving) return
    run(() => api.updateDimension(dim.id, { displayName: next, isEnabled: dim.isEnabled, ...(dim.origin === 'system' ? {} : { isRemovable: dim.isRemovable }) }))
  }

  if (locked) {
    return (
      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium">Display name</span>
        <p className="flex h-10 items-center text-base">{dim.displayName}</p>
        <p className="text-xs text-muted-foreground">Managed by {dim.profile?.owner}.</p>
      </div>
    )
  }

  return (
    <Field label="Display name" htmlFor="dim-display" errors={errorsFor('displayName')} required>
      <Input
        id="dim-display"
        value={name}
        onChange={(e) => setName(e.target.value)}
        onBlur={stage}
        onKeyDown={(e) => {
          if (e.key === 'Escape') setName(dim.displayName)
          if (e.key !== 'Enter' || e.nativeEvent.isComposing || e.keyCode === 229) return
          e.preventDefault()
          stage()
        }}
        className="h-10 text-base"
      />
      <p className="text-xs text-muted-foreground">Shown in pickers, datasets and reports. Applies to every version.</p>
    </Field>
  )
}

/** Secondary, dimension-level properties. Toggles save immediately. */
function DetailsSettings({ dim }: { dim: SectionProps['dim'] }) {
  const api = useDimensionsApi()
  const { run, saving } = useDimCall()
  const system = dim.origin === 'system'
  const locked = dim.profile?.editPolicy === 'locked'
  const update = (patch: { isEnabled?: boolean; isRemovable?: boolean }, message: string) =>
    run(() => api.updateDimension(dim.id, { displayName: dim.displayName, isEnabled: dim.isEnabled, ...(system ? {} : { isRemovable: dim.isRemovable }), ...patch }), message)

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Details</h2>
      <div className="flex flex-col divide-y rounded-lg border">
        <div className="grid grid-cols-2 gap-4 p-4">
          <ReadValue label="Key" mono>
            {dim.key}
          </ReadValue>
          <ReadValue label="Kind">{KIND_LABEL[dim.kind]}</ReadValue>
        </div>
        <label className="flex items-center justify-between gap-3 p-4 text-sm">
          <span className="flex flex-col">
            Enabled <span className="text-xs text-muted-foreground">Disabled dimensions are hidden from pickers in datasets and reports.</span>
          </span>
          <Switch checked={dim.isEnabled} onCheckedChange={(c) => update({ isEnabled: c }, c ? 'Dimension enabled' : 'Dimension disabled')} disabled={locked || saving} />
        </label>
        {!system && (
          <label className="flex items-center justify-between gap-3 p-4 text-sm">
            <span className="flex flex-col">
              Removable <span className="text-xs text-muted-foreground">Off prevents deleting the dimension even when nothing references it.</span>
            </span>
            <Switch checked={dim.isRemovable} onCheckedChange={(c) => update({ isRemovable: c }, c ? 'Dimension can be removed' : 'Dimension protected from removal')} disabled={locked || saving} />
          </label>
        )}
      </div>
      <p className="text-xs text-muted-foreground">Key and kind are fixed once created.</p>
    </section>
  )
}
