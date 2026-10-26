import { CircleCheckIcon, CircleXIcon, GripVerticalIcon, Link2Icon, Link2OffIcon, PlusIcon, Trash2Icon, TriangleAlertIcon, UploadIcon, WandSparklesIcon, XIcon } from 'lucide-react'
import { Fragment, useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { useDimensionsApi } from '@/lib/dimensions/client'
import type { Bound, ImportMode, RangeBand, RangeDataType } from '@/lib/dimensions/contract'
import { analyzeBands, bandPreview, boundError, boundScale, compareBound, formatBound, insertBandAfter, parseBound, patchBandLinked, removeBandLinked, shiftIsoDate, type BandIssue } from '@/lib/dimensions/bands'
import { cn } from '@/lib/utils'
import type { SectionProps } from './dimension-detail'
import { BandGenerator, type BandPlacement } from './band-generator'
import { BandImportDialog } from './band-import-dialog'
import { EmptyState, Mono, TabIntro, useDimCall } from './shared'

const renumber = (bands: RangeBand[]) => bands.map((b, i) => ({ ...b, ordinal: i + 1 }))

export function BandsSection({ content, editable, focusId, clearFocus }: SectionProps) {
  const api = useDimensionsApi()
  const { run, saving } = useDimCall()
  const type = content.version.rangeDataType
  const saved = useMemo(() => [...content.bands].sort((a, b) => a.ordinal - b.ordinal), [content.bands])
  const savedKey = JSON.stringify(saved)
  const [bands, setBands] = useState(saved)
  const [dragAt, setDragAt] = useState<number | null>(null)
  const [highlight, setHighlight] = useState<number | null>(null)
  const [linked, setLinked] = useState(true)
  const [generating, setGenerating] = useState(false)
  const [importing, setImporting] = useState(false)
  const [confirmingClear, setConfirmingClear] = useState(false)
  const [rejected, setRejected] = useState<Record<string, { ordinal: number; message: string }>>({})
  const setRejection = (key: string, ordinal: number, message: string | null) =>
    setRejected((r) => {
      if (!message && !(key in r)) return r
      const next = { ...r }
      if (message) next[key] = { ordinal, message }
      else delete next[key]
      return next
    })
  // Reset only when the saved bands change, so creating members for new bands keeps the pending edits.
  useEffect(() => setBands(saved), [savedKey]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (!focusId) return
    setHighlight(Number(focusId))
    document.getElementById(`band-${focusId}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' })
    clearFocus()
  }, [focusId, clearFocus])

  const memberIds = useMemo(() => new Set(content.members.map((m) => m.id)), [content.members])
  const issues = useMemo(() => analyzeBands(bands, type, memberIds), [bands, type, memberIds])
  const dirty = JSON.stringify(bands) !== JSON.stringify(saved)
  const memberOptions = content.members.map((m) => ({ value: m.id, label: m.displayName, hint: m.key }))
  const update = (i: number, patch: Partial<RangeBand>) => setBands((bs) => patchBandLinked(bs, i, patch, linked))

  if (!type) return <EmptyState title="No range data type">Set the range data type in Properties before adding bands.</EmptyState>

  const unusedMemberId = (list: RangeBand[]) => {
    const used = new Set(list.map((b) => b.memberId))
    return (content.members.find((m) => !used.has(m.id)) ?? content.members[0])?.id ?? ''
  }

  function addBand() {
    const last = bands[bands.length - 1]
    setBands(renumber([...bands, { ordinal: 0, memberId: unusedMemberId(bands), lower: last?.upper ?? null, upper: null, lowerInclusive: last ? !last.upperInclusive : true, upperInclusive: false }]))
  }

  function insertAfter(i: number) {
    const next = insertBandAfter(bands, i, type!, unusedMemberId(bands))
    setBands(next)
    setHighlight(i + 2)
  }

  function placeBands(incoming: RangeBand[], placement: BandPlacement) {
    setBands(renumber(placement === 'replace' ? incoming : [...bands, ...incoming]))
    setGenerating(false)
  }

  function importBands(incoming: RangeBand[], mode: ImportMode) {
    if (mode === 'replace') return setBands(renumber(incoming))
    const byMember = new Map(incoming.map((b) => [b.memberId, b]))
    const kept = bands.map((b) => byMember.get(b.memberId) ?? b)
    const fresh = incoming.filter((b) => !bands.some((x) => x.memberId === b.memberId))
    const lowerOf = (b: RangeBand) => b.lower
    const merged = [...kept, ...fresh].sort((a, b) => {
      const la = lowerOf(a)
      const lb = lowerOf(b)
      if (la === null || la === '') return lb === null || lb === '' ? 0 : -1
      if (lb === null || lb === '') return 1
      return compareBound(la, lb)
    })
    setBands(renumber(merged))
  }

  const tools = editable && (
    <div className="flex flex-wrap items-center gap-2">
      <Button variant={generating ? 'secondary' : 'outline'} size="sm" onClick={() => setGenerating((g) => !g)} aria-expanded={generating}>
        <WandSparklesIcon /> Generate…
      </Button>
      <Button variant="outline" size="sm" onClick={() => setImporting(true)}>
        <UploadIcon /> Import…
      </Button>
      {bands.length > 1 && (
        <Tooltip>
          <TooltipTrigger
            render={
              <Button variant={linked ? 'secondary' : 'ghost'} size="sm" aria-pressed={linked} onClick={() => setLinked((l) => !l)}>
                {linked ? <Link2Icon /> : <Link2OffIcon />} {linked ? 'Bounds linked' : 'Bounds independent'}
              </Button>
            }
          />
          <TooltipContent>{linked ? 'Editing, inserting or removing a band adjusts its neighbours so coverage stays contiguous.' : 'Each band is edited on its own; neighbours stay as they are.'}</TooltipContent>
        </Tooltip>
      )}
      {bands.length > 0 && (
        <Button variant="ghost" size="sm" className="ml-auto text-muted-foreground hover:text-destructive" onClick={() => setConfirmingClear(true)}>
          <Trash2Icon /> Clear all
        </Button>
      )}
      <Dialog open={confirmingClear} onOpenChange={setConfirmingClear}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Clear all range bands?</DialogTitle>
            <DialogDescription>
              All {bands.length} band{bands.length === 1 ? '' : 's'} will be removed. Nothing is saved until you apply your changes, and you can discard to restore them.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmingClear(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                setBands([])
                setConfirmingClear(false)
              }}
            >
              Clear all
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )

  return (
    <div className="flex flex-col gap-4">
      <TabIntro>Bands are evaluated in order and must not overlap. An empty bound is unbounded. Only the first band may be open below and only the last open above.</TabIntro>

      {tools}

      {generating && (
        <BandGenerator versionId={content.version.id} type={type} members={content.members} hasBands={bands.length > 0} onClose={() => setGenerating(false)} onGenerate={placeBands} />
      )}

      <BandImportDialog open={importing} onOpenChange={setImporting} versionId={content.version.id} type={type} members={content.members} hasBands={bands.length > 0} onImport={importBands} />

      <BandStrip bands={bands} type={type} issues={issues} names={new Map(content.members.map((m) => [m.id, m.displayName]))} />

      {!bands.length ? (
        <EmptyState
          title="No bands"
          action={
            editable && (
              <Button size="sm" onClick={addBand}>
                <PlusIcon /> Add band
              </Button>
            )
          }
        >
          Each band maps a span of values to one member. Add one by hand, generate a set from a range, or import a list.
        </EmptyState>
      ) : (
        <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <ol className="flex min-w-0 flex-col">
          {bands.map((b, i) => {
            const bad = issues.some((iss) => iss.ordinals.includes(b.ordinal) && iss.kind !== 'gap')
            const member = content.members.find((m) => m.id === b.memberId)
            return (
              <Fragment key={i}>
              <li
                key={i}
                id={`band-${b.ordinal}`}
                draggable={editable}
                onDragStart={() => setDragAt(i)}
                onDragEnd={() => setDragAt(null)}
                onDragOver={(e) => dragAt !== null && e.preventDefault()}
                onDrop={(e) => {
                  e.preventDefault()
                  if (dragAt === null || dragAt === i) return
                  const next = [...bands]
                  const [moved] = next.splice(dragAt, 1)
                  next.splice(i, 0, moved)
                  setBands(renumber(next))
                  setDragAt(null)
                }}
                className={cn(
                  'flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3',
                  bad && 'border-destructive/60',
                  highlight === b.ordinal && 'ring-2 ring-primary',
                  dragAt === i && 'opacity-40',
                )}
              >
                <div className="flex items-center gap-1.5 self-center">
                  {editable && <GripVerticalIcon aria-hidden className="size-4 cursor-grab text-muted-foreground/60" />}
                  <span className="w-6 font-mono text-sm text-muted-foreground tabular-nums">{b.ordinal}</span>
                </div>
                {editable ? (
                  <>
                    <label className="flex w-48 flex-col gap-1 text-xs text-muted-foreground">
                      Member
                      <SimpleSelect value={b.memberId} onChange={(v) => update(i, { memberId: v })} options={memberOptions} placeholder="Choose…" size="sm" aria-label={`Band ${b.ordinal} member`} />
                    </label>
                    <BoundInput
                      label="Lower bound"
                      type={type}
                      value={b.lower}
                      max={type === 'date' && typeof b.upper === 'string' && b.upper ? shiftIsoDate(b.upper, -1) : undefined}
                      validate={(v) => boundError(bands, i, 'lower', v, linked, type)}
                      onReject={(msg) => setRejection(`${i}-lower`, b.ordinal, msg)}
                      onChange={(v) => update(i, { lower: v })}
                    />
                    <BoundInput
                      label="Upper bound"
                      type={type}
                      value={b.upper}
                      min={type === 'date' && typeof b.lower === 'string' && b.lower ? shiftIsoDate(b.lower, 1) : undefined}
                      validate={(v) => boundError(bands, i, 'upper', v, linked, type)}
                      onReject={(msg) => setRejection(`${i}-upper`, b.ordinal, msg)}
                      onChange={(v) => update(i, { upper: v })}
                    />
                    <label className="flex h-8 items-center gap-2 text-sm">
                      <Checkbox checked={b.lowerInclusive} onCheckedChange={(v) => update(i, { lowerInclusive: !!v })} /> Include lower
                    </label>
                    <label className="flex h-8 items-center gap-2 text-sm">
                      <Checkbox checked={b.upperInclusive} onCheckedChange={(v) => update(i, { upperInclusive: !!v })} /> Include upper
                    </label>
                  </>
                ) : (
                  <span className="self-center text-sm">
                    {member?.displayName ?? <span className="text-destructive">Unknown member</span>} {member && <Mono>{member.key}</Mono>}
                  </span>
                )}
                <code className="ml-auto self-center rounded bg-muted px-2 py-1 font-mono text-xs">{bandPreview(b, type)}</code>
                {editable && (
                  <Button variant="ghost" size="icon-sm" className="self-center" aria-label={`Remove band ${b.ordinal}`} onClick={() => setBands(removeBandLinked(bands, i, linked))}>
                    <XIcon />
                  </Button>
                )}
              </li>
              {editable ? (
                <InsertGap
                  label={i === bands.length - 1 ? `Add band after ${b.ordinal}` : `Insert band between ${b.ordinal} and ${b.ordinal + 1}`}
                  hint={i === bands.length - 1 ? (b.upper === null ? 'splits the open top band' : 'continues from the last bound') : 'splits band ' + b.ordinal}
                  onInsert={() => (i === bands.length - 1 && b.upper !== null ? addBand() : insertAfter(i))}
                />
              ) : (
                <li aria-hidden className="h-2" />
              )}
              </Fragment>
            )
          })}
        </ol>
        <BandChecks
          issues={issues}
          rejected={Object.values(rejected)}
          onFocus={(ordinal) => {
            setHighlight(ordinal)
            document.getElementById(`band-${ordinal}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' })
          }}
        />
        </div>
      )}

      {editable && (bands.length > 0 || dirty) && (
        <div className="flex items-center gap-2">
          {bands.length > 0 && (
            <Button variant="outline" size="sm" onClick={addBand}>
              <PlusIcon /> Add band
            </Button>
          )}
          <span className="flex-1" />
          {dirty && <span className="text-xs text-warning-foreground">Unsaved band changes</span>}
          <Button variant="ghost" size="sm" disabled={!dirty} onClick={() => setBands(saved)}>
            Discard
          </Button>
          <Button size="sm" disabled={!dirty || saving} onClick={() => run(() => api.saveBands(content.version.id, bands), `Saved ${bands.length} bands`)}>
            {saving ? 'Applying…' : 'Apply bands'}
          </Button>
        </div>
      )}
    </div>
  )
}

/** Hover strip between two bands; inserting splits the band above so the ranges around it adjust. */
function InsertGap({ label, hint, onInsert }: { label: string; hint: string; onInsert: () => void }) {
  return (
    <li className="group/gap relative flex h-4 items-center justify-center">
      <span aria-hidden className="absolute inset-x-3 top-1/2 h-px bg-primary opacity-0 transition-opacity group-focus-within/gap:opacity-60 group-hover/gap:opacity-60" />
      <button
        type="button"
        onClick={onInsert}
        aria-label={label}
        className="relative z-10 flex items-center gap-1 rounded-full border border-primary/50 bg-background px-2 py-0.5 text-[11px] text-primary opacity-0 transition-opacity group-hover/gap:opacity-100 focus-visible:opacity-100 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <PlusIcon aria-hidden className="size-3" /> Insert <span className="text-muted-foreground">· {hint}</span>
      </button>
    </li>
  )
}

/**
 * A bound field that never commits an invalid value: rejected input stays visible (marked invalid,
 * reason listed in the checks panel) until corrected, and reverts to the last valid value on blur.
 */
function BoundInput({
  label,
  type,
  value,
  min,
  max,
  validate,
  onReject,
  onChange,
}: {
  label: string
  type: RangeDataType
  value: Bound
  min?: string
  max?: string
  validate: (v: Bound) => string | null
  onReject: (message: string | null) => void
  onChange: (v: Bound) => void
}) {
  const committed = value === null ? '' : String(value)
  const [raw, setRaw] = useState(committed)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    setRaw(committed)
    setError(null)
  }, [committed])
  useEffect(() => () => onReject(null), []) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <label className="flex w-44 flex-col gap-1 text-xs text-muted-foreground">
      {label}
      <Input
        type={type === 'date' ? 'date' : 'text'}
        inputMode={type === 'decimal' ? 'decimal' : undefined}
        className="h-8 font-mono text-sm text-foreground"
        placeholder={type === 'decimal' ? 'Unbounded' : undefined}
        min={min}
        max={max}
        value={raw}
        aria-invalid={error ? true : undefined}
        title={error ?? undefined}
        onChange={(e) => {
          const next = parseBound(e.target.value, type)
          const msg = validate(next)
          setRaw(e.target.value)
          setError(msg)
          onReject(msg)
          if (!msg) onChange(next)
        }}
        onBlur={() => {
          if (!error) return
          setRaw(committed)
          setError(null)
          onReject(null)
        }}
      />
    </label>
  )
}

/** Fixed-width side panel listing coverage issues and rejected edits, so rows never shift as issues come and go. */
function BandChecks({ issues, rejected, onFocus }: { issues: BandIssue[]; rejected: { ordinal: number; message: string }[]; onFocus: (ordinal: number) => void }) {
  const total = issues.length + rejected.length
  return (
    <aside aria-label="Band checks" className="flex flex-col gap-2 rounded-lg border bg-card p-3 xl:sticky xl:top-4">
      <h3 className="flex items-center justify-between text-xs font-medium tracking-wide text-muted-foreground uppercase">
        Checks
        <span className={cn('font-mono tabular-nums normal-case', total ? 'text-destructive' : 'text-muted-foreground')}>{total ? `${total} issue${total === 1 ? '' : 's'}` : 'OK'}</span>
      </h3>
      {total === 0 ? (
        <p className="flex items-start gap-2 text-sm text-muted-foreground">
          <CircleCheckIcon aria-hidden className="mt-0.5 size-4 shrink-0 text-primary" />
          Bands are ordered, contiguous and do not overlap.
        </p>
      ) : (
        <ul aria-live="polite" className="flex max-h-[60vh] flex-col gap-1 overflow-y-auto">
          {rejected.map((r, i) => (
            <CheckItem key={`r${i}`} tone="error" icon={CircleXIcon} label={`Band ${r.ordinal} · edit not applied`} message={r.message} onClick={() => onFocus(r.ordinal)} />
          ))}
          {issues.map((iss, i) => (
            <CheckItem
              key={`i${i}`}
              tone={iss.kind === 'gap' ? 'warning' : 'error'}
              icon={TriangleAlertIcon}
              label={iss.ordinals.length ? `Band ${iss.ordinals.join(', ')}` : undefined}
              message={iss.message}
              onClick={iss.ordinals.length ? () => onFocus(iss.ordinals[0]) : undefined}
            />
          ))}
        </ul>
      )}
    </aside>
  )
}

function CheckItem({ tone, icon: Icon, label, message, onClick }: { tone: 'error' | 'warning'; icon: typeof TriangleAlertIcon; label?: string; message: string; onClick?: () => void }) {
  const body = (
    <>
      <Icon aria-hidden className={cn('mt-0.5 size-3.5 shrink-0', tone === 'error' ? 'text-destructive' : 'text-warning-foreground')} />
      <span className="flex min-w-0 flex-col">
        {label && <span className="text-xs text-muted-foreground">{label}</span>}
        <span className={tone === 'error' ? 'text-destructive' : 'text-warning-foreground'}>{message}</span>
      </span>
    </>
  )
  return (
    <li>
      {onClick ? (
        <button type="button" onClick={onClick} className="flex w-full items-start gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none">
          {body}
        </button>
      ) : (
        <div className="flex items-start gap-2 px-2 py-1.5 text-sm">{body}</div>
      )}
    </li>
  )
}

/** Bands laid out on an ordinal scale of their distinct bounds, with overlaps in red and gaps in amber. */
function BandStrip({ bands, type, issues, names }: { bands: RangeBand[]; type: RangeDataType; issues: BandIssue[]; names: Map<string, string> }) {
  const scale = boundScale(bands)
  const slots = scale.length + 1
  const pos = (v: Bound, side: 'lo' | 'hi') => {
    if (v === null || v === '') return side === 'lo' ? 0 : slots
    const i = scale.findIndex((x) => compareBound(x, v as string | number) === 0)
    return i + 0.5 + 0.5
  }
  const pct = (p: number) => `${(p / slots) * 100}%`
  const spans = (kind: 'overlap' | 'gap') => issues.filter((i) => i.kind === kind && i.from !== undefined)

  if (!bands.length) return null
  return (
    <figure aria-label="Band coverage" className="flex flex-col gap-1 rounded-lg border bg-card px-4 pt-4 pb-2">
      <div className="relative h-10">
        <div aria-hidden className="absolute inset-x-0 top-1/2 h-px bg-border" />
        {bands.map((b, i) => {
          const l = pos(b.lower, 'lo')
          const r = pos(b.upper, 'hi')
          const lo = Math.min(l, r)
          const w = Math.max(Math.abs(r - l), 0.15)
          return (
            <div
              key={i}
              title={`${b.ordinal}. ${names.get(b.memberId) ?? '?'} ${bandPreview(b, type)}`}
              className={cn('absolute top-1 flex h-8 items-center justify-center overflow-hidden rounded-sm border px-1 text-[11px] whitespace-nowrap', i % 2 ? 'border-primary/40 bg-primary/15' : 'border-primary/50 bg-primary/25', r < l && 'border-destructive bg-destructive/20')}
              style={{ left: pct(lo), width: pct(w) }}
            >
              <span className="truncate">{names.get(b.memberId) ?? `Band ${b.ordinal}`}</span>
            </div>
          )
        })}
        {spans('overlap').map((s, i) => (
          <div key={`o${i}`} aria-hidden className="absolute top-0 h-10 rounded-sm border-2 border-destructive bg-destructive/30" style={{ left: pct(pos(s.from!, 'lo')), width: pct(Math.max(pos(s.to!, 'hi') - pos(s.from!, 'lo'), 0.15)) }} />
        ))}
        {spans('gap').map((s, i) => (
          <div key={`g${i}`} aria-hidden className="absolute top-0 h-10 rounded-sm border-2 border-dashed border-warning bg-warning/20" style={{ left: pct(pos(s.from!, 'lo')), width: pct(Math.max(pos(s.to!, 'hi') - pos(s.from!, 'lo'), 0.15)) }} />
        ))}
      </div>
      <div className="relative h-4 font-mono text-[10px] text-muted-foreground">
        <span className="absolute left-0">−∞</span>
        {scale.map((v, i) => (
          <span key={i} className="absolute -translate-x-1/2 whitespace-nowrap" style={{ left: pct(i + 1) }}>
            {formatBound(v, type)}
          </span>
        ))}
        <span className="absolute right-0">+∞</span>
      </div>
    </figure>
  )
}
