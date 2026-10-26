import { ExternalLinkIcon, MapPinIcon, PlusIcon, XIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { CommitInput, CommitTextarea, PanelSection, PropertiesPanel } from '@/components/shell/inline-table'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import { SearchCombobox } from '@/components/report/shared/search-combobox'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useDimensionsApi, useRefreshDimensions } from '@/lib/dimensions/client'
import type { DimensionDetail, Member, MemberInput, PropertyField, VersionContent } from '@/lib/dimensions/contract'
import { ancestorPath, descendantIds, indexTree } from '@/lib/dimensions/tree'
import { Field, ReadValue, RequiredLock } from './shared'

const ROOT = '__root__'
const NONE = '__none__'

export const toMemberInput = (m: Member): MemberInput => ({
  id: m.id,
  key: m.key,
  displayName: m.displayName,
  description: m.description,
  parentId: m.parentId,
  levelId: m.levelId,
  isAssignable: m.isAssignable,
  sortOrder: m.sortOrder,
  properties: m.properties,
})

/** Stages one field change on a member. Returns field errors so a caller can show them in place. */
export function usePatchMember(versionId: string) {
  const api = useDimensionsApi()
  const refresh = useRefreshDimensions()
  return async (m: Member, patch: Partial<MemberInput>) => {
    const r = await api.saveMember(versionId, { ...toMemberInput(m), ...patch })
    if (!r.ok) {
      const first = Object.values(r.problem.errors).flat()[0]
      toast.error(r.problem.title, { description: first ?? r.problem.detail })
      return r.problem.errors
    }
    await refresh()
    return null
  }
}

interface Props {
  dim: DimensionDetail
  content: VersionContent
  editable: boolean
  member: Member
  onClose: () => void
}

/** Docked properties for the focused member. Every change stages immediately; the header batch-saves. */
export function MemberPanel({ dim, content, editable, member, onClose }: Props) {
  const patch = usePatchMember(content.version.id)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const index = useMemo(() => indexTree(content.members), [content.members])
  const hierarchical = dim.kind === 'hierarchical'
  const schema = dim.profile?.memberPropertySchema ?? []
  const typed = dim.origin === 'system'
  const required = !!dim.profile?.requiredMemberKeys.includes(member.key)

  const set = async (p: Partial<MemberInput>) => setErrors((await patch(member, p)) ?? {})
  const errorsFor = (k: string) => errors[k]

  const parentOptions = useMemo(() => {
    const excluded = new Set([member.id, ...descendantIds(index, member.id)])
    return [
      { value: ROOT, label: 'None (root)' },
      ...content.members
        .filter((m) => !excluded.has(m.id))
        .map((m) => ({ value: m.id, label: m.displayName, hint: m.key, depth: index.depth.get(m.id) ?? 0, path: ancestorPath(index, m.id).map((a) => a.displayName).join(' › ') })),
    ]
  }, [content.members, index, member.id])

  const parent = member.parentId ? index.byId.get(member.parentId) : null
  const level = content.levels.find((l) => l.id === member.levelId)
  const path = ancestorPath(index, member.id)

  return (
    <PropertiesPanel
      label={`${member.displayName} properties`}
      title={member.displayName}
      subtitle={
        <span className="flex items-center gap-1.5">
          <span className="font-mono">{member.key}</span>
          {required && <RequiredLock what="Required by the profile" />}
          {hierarchical && path.length > 0 && <span className="truncate">· {path.map((a) => a.displayName).join(' › ')}</span>}
        </span>
      }
      onClose={onClose}
    >
      {!editable ? (
        <div className="grid grid-cols-2 gap-4">
          <ReadValue label="Display name">{member.displayName}</ReadValue>
          <ReadValue label="Key" mono>
            {member.key}
          </ReadValue>
          <div className="col-span-2">
            <ReadValue label="Description">{member.description}</ReadValue>
          </div>
          {hierarchical && <ReadValue label="Parent">{parent ? `${parent.displayName} (${parent.key})` : 'None (root)'}</ReadValue>}
          <ReadValue label="Level">{level?.displayName ?? 'None'}</ReadValue>
          <ReadValue label="Assignable">{member.isAssignable ? 'Yes' : 'No — folder'}</ReadValue>
          <ReadValue label="Sort order">{member.sortOrder}</ReadValue>
        </div>
      ) : (
        <PanelSection title="General">
          <Field label="Display name" htmlFor="member-name" errors={errorsFor('displayName')} required>
            <CommitInput id="member-name" value={member.displayName} onCommit={(v) => set({ displayName: v.trim() })} />
          </Field>
          <Field label="Description" htmlFor="member-desc">
            <CommitTextarea id="member-desc" rows={2} value={member.description ?? ''} onCommit={(v) => set({ description: v.trim() || null })} />
          </Field>
          {hierarchical && (
            <Field label="Parent" htmlFor="member-parent" errors={errorsFor('parentId')}>
              <SearchCombobox id="member-parent" value={member.parentId ?? ROOT} onChange={(v) => set({ parentId: v === ROOT ? null : v })} options={parentOptions} limit={200} searchPlaceholder="Search members…" />
            </Field>
          )}
          <div className="grid grid-cols-2 gap-3">
            <Field label="Level" htmlFor="member-level">
              <SimpleSelect
                id="member-level"
                value={member.levelId ?? NONE}
                onChange={(v) => set({ levelId: v === NONE ? null : v })}
                options={[{ value: NONE, label: 'None' }, ...content.levels.map((l) => ({ value: l.id, label: l.displayName, hint: l.key }))]}
              />
            </Field>
            <Field label="Sort order" htmlFor="member-sort">
              <CommitInput id="member-sort" type="number" min={1} value={member.sortOrder == null ? '' : String(member.sortOrder)} onCommit={(v) => set({ sortOrder: v ? Number(v) : null })} />
            </Field>
          </div>
          <label className="flex items-center justify-between gap-3 text-sm">
            <span className="flex flex-col">
              Assignable <span className="text-xs text-muted-foreground">Off for folder nodes that only group other members.</span>
            </span>
            <Switch checked={member.isAssignable} onCheckedChange={(v) => set({ isAssignable: v })} />
          </label>
        </PanelSection>
      )}

      {typed && schema.length > 0 && (
        <PanelSection title="Properties">
          {schema.map((f) => (
            <TypedProperty key={f.key} field={f} value={member.properties[f.key]} editable={editable} errors={errorsFor(`properties.${f.key}`)} onChange={(v) => set({ properties: { ...member.properties, [f.key]: v } })} />
          ))}
        </PanelSection>
      )}
      {!typed && <CustomProperties value={member.properties} editable={editable} onChange={(v) => set({ properties: v })} />}
    </PropertiesPanel>
  )
}

function TypedProperty({ field, value, editable, errors, onChange }: { field: PropertyField; value: unknown; editable: boolean; errors?: string[]; onChange: (v: unknown) => void }) {
  const id = `prop-${field.key}`
  const str = value === undefined || value === null ? '' : String(value)

  if (!editable) {
    if (field.type === 'image' && str)
      return (
        <ReadValue label={field.label}>
          <img src={str} alt={field.label} className="size-20 rounded-md border object-cover" />
        </ReadValue>
      )
    if (field.type === 'geo') {
      const g = value as { lat?: number; lng?: number } | undefined
      return <ReadValue label={field.label}>{g?.lat !== undefined ? `${g.lat}, ${g.lng}` : null}</ReadValue>
    }
    if (field.type === 'boolean') return <ReadValue label={field.label}>{value === undefined ? null : value ? 'Yes' : 'No'}</ReadValue>
    if (field.format === 'geojson') return <ReadValue label={field.label}>{str ? `GeoJSON · ${str.length.toLocaleString()} characters` : null}</ReadValue>
    return <ReadValue label={field.label}>{str || null}</ReadValue>
  }

  const text = (type?: string, extra?: { placeholder?: string; className?: string }) => <CommitInput id={id} type={type} value={str} onCommit={(v) => onChange(v === '' ? undefined : type === 'number' ? Number(v) : v)} {...extra} />

  switch (field.type) {
    case 'number':
      return (
        <Field label={field.label} htmlFor={id} hint={field.description} errors={errors} required={field.required}>
          {text('number')}
        </Field>
      )
    case 'date':
      return (
        <Field label={field.label} htmlFor={id} hint={field.description} errors={errors} required={field.required}>
          {text('date', { className: 'w-fit' })}
        </Field>
      )
    case 'url':
      return (
        <Field label={field.label} htmlFor={id} hint={field.description} errors={errors} required={field.required}>
          <div className="flex gap-2">
            {text('url', { placeholder: 'https://' })}
            {str && (
              <Button variant="outline" size="icon" render={<a href={str} target="_blank" rel="noreferrer" aria-label="Open link" />}>
                <ExternalLinkIcon />
              </Button>
            )}
          </div>
        </Field>
      )
    case 'image':
      return (
        <Field label={field.label} htmlFor={id} hint={field.description ?? 'Image URL.'} errors={errors} required={field.required}>
          <div className="flex items-center gap-3">
            {str ? <img src={str} alt="" className="size-12 shrink-0 rounded-md border object-cover" /> : <span className="size-12 shrink-0 rounded-md border border-dashed" />}
            {text(undefined, { placeholder: '/images/…' })}
          </div>
        </Field>
      )
    case 'select':
      return (
        <Field label={field.label} htmlFor={id} hint={field.description} errors={errors} required={field.required}>
          <SimpleSelect id={id} value={str} onChange={onChange} options={(field.options ?? []).map((o) => ({ value: o, label: o }))} placeholder="Choose…" />
        </Field>
      )
    case 'boolean':
      return (
        <label className="flex items-center justify-between gap-3 text-sm">
          <span>
            {field.label}
            {field.description && <span className="block text-xs text-muted-foreground">{field.description}</span>}
          </span>
          <Switch checked={!!value} onCheckedChange={onChange} />
        </label>
      )
    case 'geo':
      return <GeoField field={field} value={value as { lat?: number; lng?: number } | undefined} errors={errors} onChange={onChange} />
    default:
      if (field.format === 'geojson')
        return (
          <Field label={field.label} htmlFor={id} errors={errors} hint={field.description ?? 'Paste a GeoJSON geometry or feature.'}>
            <CommitTextarea id={id} rows={5} className="font-mono text-xs" value={str} onCommit={(v) => onChange(v || undefined)} spellCheck={false} />
          </Field>
        )
      return (
        <Field label={field.label} htmlFor={id} hint={field.description} errors={errors} required={field.required}>
          {text()}
        </Field>
      )
  }
}

function GeoField({ field, value, errors, onChange }: { field: PropertyField; value: { lat?: number; lng?: number } | undefined; errors?: string[]; onChange: (v: unknown) => void }) {
  const lat = value?.lat
  const lng = value?.lng
  const setPart = (part: 'lat' | 'lng', raw: string) => onChange({ ...value, [part]: raw === '' ? undefined : Number(raw) })
  const valid = typeof lat === 'number' && typeof lng === 'number' && Math.abs(lat) <= 90 && Math.abs(lng) <= 180
  return (
    <Field label={field.label} hint={field.description} errors={errors} required={field.required}>
      <div className="flex gap-3">
        <div className="grid flex-1 grid-cols-2 gap-2">
          <CommitInput type="number" step="any" aria-label={`${field.label} latitude`} placeholder="Latitude" value={lat == null ? '' : String(lat)} onCommit={(v) => setPart('lat', v)} />
          <CommitInput type="number" step="any" aria-label={`${field.label} longitude`} placeholder="Longitude" value={lng == null ? '' : String(lng)} onCommit={(v) => setPart('lng', v)} />
        </div>
        <a
          href={valid ? `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=14/${lat}/${lng}` : undefined}
          target="_blank"
          rel="noreferrer"
          aria-label="Preview location on a map"
          className="relative flex h-16 w-20 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted bg-[linear-gradient(var(--border)_1px,transparent_1px),linear-gradient(90deg,var(--border)_1px,transparent_1px)] bg-[size:12px_12px]"
        >
          {valid ? (
            <MapPinIcon aria-hidden className="absolute size-5 -translate-x-1/2 -translate-y-full fill-primary/30 text-primary" style={{ left: `${((lng + 180) / 360) * 100}%`, top: `${((90 - lat) / 180) * 100}%` }} />
          ) : (
            <span className="text-[10px] text-muted-foreground">No point</span>
          )}
        </a>
      </div>
    </Field>
  )
}

function CustomProperties({ value, editable, onChange }: { value: Record<string, unknown>; editable: boolean; onChange: (v: Record<string, unknown>) => void }) {
  const entries = Object.entries(value)
  const [showJson, setShowJson] = useState(false)
  const [jsonError, setJsonError] = useState<string | null>(null)
  const display = (v: unknown) => (typeof v === 'string' ? v : JSON.stringify(v))
  const rename = (from: string, to: string) => to && to !== from && !(to in value) && onChange(Object.fromEntries(entries.map(([k, v]) => [k === from ? to : k, v])))
  const parseValue = (raw: string): unknown => {
    try {
      return JSON.parse(raw)
    } catch {
      return raw
    }
  }

  return (
    <PanelSection
      title={`Custom properties · ${entries.length}`}
      action={
        <Button variant="ghost" size="xs" onClick={() => (setJsonError(null), setShowJson((s) => !s))}>
          {showJson ? 'View fields' : 'View JSON'}
        </Button>
      }
    >
      {showJson ? (
        <>
          <CommitTextarea
            rows={8}
            className="font-mono text-xs"
            value={JSON.stringify(value, null, 2)}
            readOnly={!editable}
            spellCheck={false}
            aria-label="Custom properties JSON"
            onCommit={(raw) => {
              try {
                const parsed = JSON.parse(raw)
                if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('Must be a JSON object.')
                setJsonError(null)
                onChange(parsed)
              } catch (err) {
                setJsonError(err instanceof Error ? err.message : 'Invalid JSON')
              }
            }}
          />
          {jsonError && <p className="text-xs text-destructive">{jsonError}</p>}
        </>
      ) : !entries.length ? (
        <p className="text-xs text-muted-foreground">No custom properties.</p>
      ) : (
        entries.map(([k, v]) =>
          editable ? (
            <div key={k} className="flex gap-2">
              <CommitInput aria-label="Property key" className="w-32 font-mono text-xs" value={k} onCommit={(to) => rename(k, to.trim())} />
              <CommitInput aria-label={`Value of ${k}`} value={display(v)} onCommit={(raw) => onChange({ ...value, [k]: parseValue(raw) })} />
              <Button
                variant="ghost"
                size="icon"
                aria-label={`Remove ${k}`}
                onClick={() => {
                  const { [k]: _, ...rest } = value
                  onChange(rest)
                }}
              >
                <XIcon />
              </Button>
            </div>
          ) : (
            <div key={k} className="flex gap-3 text-sm">
              <span className="w-32 shrink-0 font-mono text-xs text-muted-foreground">{k}</span>
              <span className="min-w-0 truncate">{display(v)}</span>
            </div>
          ),
        )
      )}
      {editable && !showJson && (
        <Button
          variant="outline"
          size="sm"
          className="w-fit"
          onClick={() => {
            let n = entries.length + 1
            while (`property${n}` in value) n++
            onChange({ ...value, [`property${n}`]: '' })
          }}
        >
          <PlusIcon /> Add property
        </Button>
      )}
    </PanelSection>
  )
}
