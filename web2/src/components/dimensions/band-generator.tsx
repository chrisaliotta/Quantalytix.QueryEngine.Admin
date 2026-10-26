import { WandSparklesIcon, XIcon } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Segmented } from '@/components/report/shared/segmented'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { keyFromName } from '@/components/shell/inline-table'
import { useDimensionsApi } from '@/lib/dimensions/client'
import type { Member, RangeBand, RangeDataType } from '@/lib/dimensions/contract'
import { bandPreview, generateBands, parseBound, type BandDraft, type BandSpec, type ClosedEnd, type StepUnit } from '@/lib/dimensions/bands'
import { cn } from '@/lib/utils'
import { useDimCall } from './shared'

export type BandPlacement = 'replace' | 'append'
type MemberSource = 'create' | 'existing'

/** Unique member keys for new band members, avoiding keys already in the version. */
export function bandMemberKeys(labels: string[], prefix: string, taken: Iterable<string>) {
  const used = new Set(taken)
  const p = keyFromName(prefix)
  return labels.map((label) => {
    const body = keyFromName(label).replace(/^K_/, '')
    const base = p ? `${p}_${body || 'BAND'}` : keyFromName(label) || 'BAND'
    let key = /^[A-Za-z]/.test(base) ? base : `B_${base}`
    for (let n = 2; used.has(key); n++) key = `${base}_${n}`
    used.add(key)
    return key
  })
}

const UNIT_OPTIONS: { value: StepUnit; label: string }[] = [
  { value: 'day', label: 'Days' },
  { value: 'month', label: 'Months' },
  { value: 'year', label: 'Years' },
]

/** Inline builder that turns a min/max span and a bucket count or step into bands, creating members as needed. */
export function BandGenerator({
  versionId,
  type,
  members,
  hasBands,
  onClose,
  onGenerate,
}: {
  versionId: string
  type: RangeDataType
  members: Member[]
  hasBands: boolean
  onClose: () => void
  onGenerate: (bands: RangeBand[], placement: BandPlacement) => void
}) {
  const api = useDimensionsApi()
  const { run, saving } = useDimCall()
  const [min, setMin] = useState(type === 'date' ? '2024-01-01' : '0')
  const [max, setMax] = useState(type === 'date' ? '2025-01-01' : '100000')
  const [mode, setMode] = useState<BandSpec['mode']>('count')
  const [count, setCount] = useState('5')
  const [step, setStep] = useState(type === 'date' ? '1' : '10000')
  const [unit, setUnit] = useState<StepUnit>('month')
  const [openBelow, setOpenBelow] = useState(false)
  const [openAbove, setOpenAbove] = useState(true)
  const [closed, setClosed] = useState<ClosedEnd>('lower')
  const [source, setSource] = useState<MemberSource>('create')
  const [prefix, setPrefix] = useState('BAND')
  const [placement, setPlacement] = useState<BandPlacement>(hasBands ? 'append' : 'replace')

  const spec: BandSpec = { type, min: parseBound(min, type), max: parseBound(max, type), mode, count: Number(count), step: Number(step), unit, openBelow, openAbove, closed }
  const { bands: drafts, error } = useMemo(() => generateBands(spec), [JSON.stringify(spec)]) // eslint-disable-line react-hooks/exhaustive-deps
  const keys = useMemo(() => bandMemberKeys(drafts.map((d) => d.label), prefix, members.map((m) => m.key)), [drafts, prefix, members])
  const tooFewMembers = source === 'existing' && drafts.length > members.length

  async function generate() {
    let ids: string[]
    if (source === 'existing') ids = members.slice(0, drafts.length).map((m) => m.id)
    else {
      const r = await run(() => api.importMembers(versionId, drafts.map((d, i) => ({ key: keys[i], displayName: d.label, parentKey: null }))), ({ added }) => `Created ${added.length} member${added.length === 1 ? '' : 's'}`)
      if (!r.ok) return
      ids = r.data.added.map((m) => m.id)
    }
    onGenerate(drafts.map((d: BandDraft, i) => ({ ordinal: 0, memberId: ids[i], lower: d.lower, upper: d.upper, lowerInclusive: d.lowerInclusive, upperInclusive: d.upperInclusive })), placement)
  }

  return (
    <section aria-label="Generate bands" className="flex flex-col gap-4 rounded-lg border bg-card p-4">
      <header className="flex items-center gap-2">
        <WandSparklesIcon aria-hidden className="size-4 text-primary" />
        <h3 className="text-sm font-medium">Generate bands</h3>
        <Button variant="ghost" size="icon-sm" className="ml-auto" aria-label="Close generator" onClick={onClose}>
          <XIcon />
        </Button>
      </header>

      <div className="flex flex-wrap items-end gap-x-4 gap-y-3">
        <Field label={type === 'date' ? 'From' : 'Minimum'}>
          <BoundField type={type} value={min} onChange={setMin} />
        </Field>
        <Field label={type === 'date' ? 'To' : 'Maximum'}>
          <BoundField type={type} value={max} onChange={setMax} />
        </Field>
        <Field label="Split by">
          <Segmented<BandSpec['mode']>
            aria-label="Split by"
            value={mode}
            onChange={setMode}
            options={[
              { value: 'count', label: 'Bucket count' },
              { value: 'step', label: 'Step size' },
            ]}
          />
        </Field>
        {mode === 'count' ? (
          <Field label="Buckets">
            <Input type="number" min={1} max={200} className="h-8 w-24 font-mono text-sm" value={count} onChange={(e) => setCount(e.target.value)} />
          </Field>
        ) : (
          <Field label="Step">
            <div className="flex items-center gap-2">
              <Input type="number" min={type === 'date' ? 1 : undefined} step="any" className="h-8 w-28 font-mono text-sm" value={step} onChange={(e) => setStep(e.target.value)} />
              {type === 'date' && <SimpleSelect size="sm" className="w-28" aria-label="Step unit" value={unit} onChange={(v) => setUnit(v as StepUnit)} options={UNIT_OPTIONS} />}
            </div>
          </Field>
        )}
        <Field label="Boundaries">
          <Segmented<ClosedEnd>
            aria-label="Boundaries"
            value={closed}
            onChange={setClosed}
            options={[
              { value: 'lower', label: '[a, b)' },
              { value: 'upper', label: '(a, b]' },
            ]}
          />
        </Field>
        <div className="flex h-8 items-center gap-4">
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={openBelow} onCheckedChange={(v) => setOpenBelow(!!v)} /> Open band below
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={openAbove} onCheckedChange={(v) => setOpenAbove(!!v)} /> Open band above
          </label>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-x-4 gap-y-3 border-t pt-3">
        <Field label="Members">
          <Segmented<MemberSource>
            aria-label="Members"
            value={source}
            onChange={setSource}
            options={[
              { value: 'create', label: 'Create new' },
              { value: 'existing', label: 'Use existing in order' },
            ]}
          />
        </Field>
        {source === 'create' && (
          <Field label="Key prefix">
            <Input className="h-8 w-32 font-mono text-sm" value={prefix} onChange={(e) => setPrefix(e.target.value)} spellCheck={false} />
          </Field>
        )}
        {hasBands && (
          <Field label="Existing bands">
            <Segmented<BandPlacement>
              aria-label="Existing bands"
              value={placement}
              onChange={setPlacement}
              options={[
                { value: 'append', label: 'Add after' },
                { value: 'replace', label: 'Replace all' },
              ]}
            />
          </Field>
        )}
      </div>

      {error ? (
        <p className="text-sm text-destructive">{error}</p>
      ) : (
        <div className="flex flex-col gap-1.5">
          <p className="text-xs text-muted-foreground">
            Preview · {drafts.length} band{drafts.length === 1 ? '' : 's'}
          </p>
          <ol className="flex max-h-40 flex-wrap gap-1.5 overflow-auto">
            {drafts.map((d, i) => (
              <li key={i} className="flex items-center gap-2 rounded-md border bg-muted/40 px-2 py-1 text-xs">
                <span className="font-medium">{source === 'existing' ? (members[i]?.displayName ?? <span className="text-destructive">No member</span>) : d.label}</span>
                <code className="font-mono text-muted-foreground">{bandPreview({ ordinal: i + 1, memberId: '', ...d }, type)}</code>
                {source === 'create' && <code className="font-mono text-muted-foreground/70">{keys[i]}</code>}
              </li>
            ))}
          </ol>
          {tooFewMembers && (
            <p className="text-sm text-destructive">
              Only {members.length} member{members.length === 1 ? '' : 's'} exist for {drafts.length} bands. Create new members or reduce the buckets.
            </p>
          )}
        </div>
      )}

      <footer className="flex items-center justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onClose}>
          Cancel
        </Button>
        <Button size="sm" disabled={!!error || !drafts.length || tooFewMembers || saving} onClick={generate}>
          {saving ? 'Creating members…' : `${placement === 'replace' && hasBands ? 'Replace with' : 'Add'} ${drafts.length || ''} band${drafts.length === 1 ? '' : 's'}`}
        </Button>
      </footer>
    </section>
  )
}

function Field({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <div className={cn('flex flex-col gap-1', className)}>
      <span className="text-xs text-muted-foreground">{label}</span>
      {children}
    </div>
  )
}

function BoundField({ type, value, onChange }: { type: RangeDataType; value: string; onChange: (v: string) => void }) {
  return (
    <Input
      type={type === 'date' ? 'date' : 'text'}
      inputMode={type === 'decimal' ? 'decimal' : undefined}
      className="h-8 w-40 font-mono text-sm"
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  )
}
