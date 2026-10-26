import type { Bound, RangeBand, RangeDataType } from './contract'

export type BandIssueKind = 'overlap' | 'gap' | 'order' | 'openLower' | 'openUpper' | 'member'

export interface BandIssue {
  kind: BandIssueKind
  /** Band ordinals involved. */
  ordinals: number[]
  message: string
  /** The span of an overlap or gap, for the band strip. */
  from?: Bound
  to?: Bound
}

const isSet = (v: Bound): v is string | number => v !== null && v !== ''

/** Compares two finite bounds of the same type. ISO date strings sort lexically. */
export function compareBound(a: string | number, b: string | number) {
  if (typeof a === 'number' && typeof b === 'number') return a - b
  return String(a) < String(b) ? -1 : String(a) > String(b) ? 1 : 0
}

export function formatBound(v: Bound, type: RangeDataType | null) {
  if (!isSet(v)) return ''
  if (type === 'decimal' && typeof v === 'number') return v.toLocaleString('en-US')
  return String(v)
}

/** e.g. `[0, 10,000)` or `(−∞, 2024-01-01)`. */
export function bandPreview(b: RangeBand, type: RangeDataType | null) {
  const lo = isSet(b.lower) ? formatBound(b.lower, type) : '−∞'
  const hi = isSet(b.upper) ? formatBound(b.upper, type) : '+∞'
  return `${isSet(b.lower) && b.lowerInclusive ? '[' : '('}${lo}, ${hi}${isSet(b.upper) && b.upperInclusive ? ']' : ')'}`
}

export function parseBound(raw: string, type: RangeDataType | null): Bound {
  const t = raw.trim()
  if (!t) return null
  if (type === 'decimal') {
    const n = Number(t.replace(/,/g, ''))
    return Number.isFinite(n) ? n : null
  }
  return t
}

/** −∞/+∞ aware comparison of endpoints. `side` says which infinity a null stands for. */
function cmpEnd(a: Bound, aSide: 'lo' | 'hi', b: Bound, bSide: 'lo' | 'hi') {
  const ai = isSet(a) ? 0 : aSide === 'lo' ? -1 : 1
  const bi = isSet(b) ? 0 : bSide === 'lo' ? -1 : 1
  if (ai || bi) return ai - bi
  return compareBound(a as string | number, b as string | number)
}

function overlapOf(a: RangeBand, b: RangeBand): { from: Bound; to: Bound } | null {
  const loIsA = cmpEnd(a.lower, 'lo', b.lower, 'lo') >= 0
  const lo = loIsA ? a.lower : b.lower
  const loInc = cmpEnd(a.lower, 'lo', b.lower, 'lo') === 0 ? a.lowerInclusive && b.lowerInclusive : loIsA ? a.lowerInclusive : b.lowerInclusive
  const hiIsA = cmpEnd(a.upper, 'hi', b.upper, 'hi') <= 0
  const hi = hiIsA ? a.upper : b.upper
  const hiInc = cmpEnd(a.upper, 'hi', b.upper, 'hi') === 0 ? a.upperInclusive && b.upperInclusive : hiIsA ? a.upperInclusive : b.upperInclusive
  const c = cmpEnd(lo, 'lo', hi, 'hi')
  if (c < 0) return { from: lo, to: hi }
  if (c === 0 && isSet(lo) && loInc && hiInc) return { from: lo, to: hi }
  return null
}

/** Every problem with a band list: overlaps, gaps, inverted bounds, misplaced open bounds and unknown members. */
export function analyzeBands(bands: RangeBand[], type: RangeDataType | null, memberIds?: Set<string>): BandIssue[] {
  const issues: BandIssue[] = []
  const sorted = [...bands].sort((a, b) => a.ordinal - b.ordinal)
  const fmt = (v: Bound) => formatBound(v, type)

  sorted.forEach((b, i) => {
    if (isSet(b.lower) && isSet(b.upper) && compareBound(b.lower, b.upper) > 0)
      issues.push({ kind: 'order', ordinals: [b.ordinal], message: `Band ${b.ordinal}: lower bound ${fmt(b.lower)} is above upper bound ${fmt(b.upper)}.` })
    if (!isSet(b.lower) && i > 0) issues.push({ kind: 'openLower', ordinals: [b.ordinal], message: `Band ${b.ordinal} is open below; only the first band may be.` })
    if (!isSet(b.upper) && i < sorted.length - 1) issues.push({ kind: 'openUpper', ordinals: [b.ordinal], message: `Band ${b.ordinal} is open above; only the last band may be.` })
    if (memberIds && !memberIds.has(b.memberId)) issues.push({ kind: 'member', ordinals: [b.ordinal], message: `Band ${b.ordinal} assigns a member that is not in this version.` })
  })

  for (let i = 0; i < sorted.length; i++)
    for (let j = i + 1; j < sorted.length; j++) {
      const o = overlapOf(sorted[i], sorted[j])
      if (o) issues.push({ kind: 'overlap', ordinals: [sorted[i].ordinal, sorted[j].ordinal], from: o.from, to: o.to, message: `Bands ${sorted[i].ordinal} and ${sorted[j].ordinal} overlap ${o.from === o.to ? `at ${fmt(o.from)}` : `from ${fmt(o.from) || '−∞'} to ${fmt(o.to) || '+∞'}`}.` })
    }

  for (let i = 0; i < sorted.length - 1; i++) {
    const a = sorted[i]
    const b = sorted[i + 1]
    if (!isSet(a.upper) || !isSet(b.lower)) continue
    const c = compareBound(a.upper, b.lower)
    if (c < 0 || (c === 0 && !a.upperInclusive && !b.lowerInclusive))
      issues.push({ kind: 'gap', ordinals: [a.ordinal, b.ordinal], from: a.upper, to: b.lower, message: c < 0 ? `Gap between bands ${a.ordinal} and ${b.ordinal}: ${fmt(a.upper)} to ${fmt(b.lower)} is unassigned.` : `Gap at ${fmt(a.upper)}: neither band ${a.ordinal} nor ${b.ordinal} includes it.` })
  }
  return issues
}

/* ───────────────────────── generation ───────────────────────── */

export type StepUnit = 'day' | 'month' | 'year'
export type ClosedEnd = 'lower' | 'upper'

export interface BandSpec {
  type: RangeDataType
  min: Bound
  max: Bound
  mode: 'count' | 'step'
  count: number
  step: number
  unit: StepUnit
  openBelow: boolean
  openAbove: boolean
  closed: ClosedEnd
}

/** A band before it has a member: the label seeds the new member's name. */
export interface BandDraft {
  lower: Bound
  upper: Bound
  lowerInclusive: boolean
  upperInclusive: boolean
  label: string
}

export const MAX_GENERATED_BANDS = 200
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/
const round = (n: number) => Math.round(n * 1e9) / 1e9

const toDate = (iso: string) => new Date(`${iso}T00:00:00Z`)
const toIso = (d: Date) => d.toISOString().slice(0, 10)
const dayDiff = (a: string, b: string) => Math.round((toDate(b).getTime() - toDate(a).getTime()) / 86_400_000)
const addDays = (iso: string, n: number) => {
  const d = toDate(iso)
  d.setUTCDate(d.getUTCDate() + n)
  return toIso(d)
}
function addUnit(iso: string, n: number, unit: StepUnit) {
  if (unit === 'day') return addDays(iso, n)
  const d = toDate(iso)
  if (unit === 'month') d.setUTCMonth(d.getUTCMonth() + n)
  else d.setUTCFullYear(d.getUTCFullYear() + n)
  return toIso(d)
}

export function isValidBound(v: Bound, type: RangeDataType) {
  if (!isSet(v)) return false
  return type === 'decimal' ? typeof v === 'number' && Number.isFinite(v) : typeof v === 'string' && ISO_DATE.test(v) && !Number.isNaN(toDate(v).getTime())
}

function labelFor(lo: Bound, hi: Bound, type: RangeDataType) {
  const f = (v: Bound) => formatBound(v, type)
  if (!isSet(lo)) return type === 'date' ? `Before ${f(hi)}` : `Under ${f(hi)}`
  if (!isSet(hi)) return type === 'date' ? `${f(lo)} onwards` : `${f(lo)} and over`
  return `${f(lo)} – ${f(hi)}`
}

const draft = (lower: Bound, upper: Bound, closed: ClosedEnd, type: RangeDataType): BandDraft => ({
  lower,
  upper,
  lowerInclusive: closed === 'lower',
  upperInclusive: closed === 'upper',
  label: labelFor(lower, upper, type),
})

/** Splits `[min, max]` into evenly sized or fixed-step bands, optionally with open bands either side. */
export function generateBands(spec: BandSpec): { bands: BandDraft[]; error?: string } {
  const { type, min, max } = spec
  if (!isValidBound(min, type) || !isValidBound(max, type)) return { bands: [], error: `Enter a valid ${type === 'date' ? 'start and end date' : 'minimum and maximum'}.` }
  if (compareBound(min as string | number, max as string | number) >= 0) return { bands: [], error: 'The maximum must be above the minimum.' }

  const edges: (string | number)[] = [min as string | number]
  if (spec.mode === 'count') {
    const n = Math.floor(spec.count)
    if (!(n >= 1)) return { bands: [], error: 'Enter at least one bucket.' }
    if (n > MAX_GENERATED_BANDS) return { bands: [], error: `Use at most ${MAX_GENERATED_BANDS} buckets.` }
    if (type === 'decimal') {
      const width = ((max as number) - (min as number)) / n
      for (let i = 1; i < n; i++) edges.push(round((min as number) + width * i))
    } else {
      const days = dayDiff(min as string, max as string)
      if (n > days) return { bands: [], error: `The range only spans ${days} day${days === 1 ? '' : 's'}.` }
      for (let i = 1; i < n; i++) edges.push(addDays(min as string, Math.round((days * i) / n)))
    }
  } else {
    if (!(spec.step > 0)) return { bands: [], error: 'Enter a step above zero.' }
    for (let i = 1; ; i++) {
      const next = type === 'decimal' ? round((min as number) + spec.step * i) : addUnit(min as string, Math.floor(spec.step) * i, spec.unit)
      if (compareBound(next, max as string | number) >= 0) break
      edges.push(next)
      if (edges.length > MAX_GENERATED_BANDS) return { bands: [], error: `That step makes more than ${MAX_GENERATED_BANDS} bands. Use a larger step.` }
    }
  }
  edges.push(max as string | number)

  const bands: BandDraft[] = []
  if (spec.openBelow) bands.push(draft(null, min, spec.closed, type))
  for (let i = 0; i < edges.length - 1; i++) bands.push(draft(edges[i], edges[i + 1], spec.closed, type))
  if (spec.openAbove) bands.push(draft(max, null, spec.closed, type))
  // The top finite band includes max when bands close upward but nothing sits above it.
  if (spec.closed === 'lower' && !spec.openAbove) bands[bands.length - 1].upperInclusive = true
  if (spec.closed === 'upper' && !spec.openBelow) bands[0].lowerInclusive = true
  return { bands }
}

/* ───────────────────────── insert / adjust ───────────────────────── */

/** Width of a finite band in its own units (days for dates). */
function widthOf(b: RangeBand, type: RangeDataType): number | null {
  if (!isSet(b.lower) || !isSet(b.upper)) return null
  return type === 'decimal' ? (b.upper as number) - (b.lower as number) : dayDiff(b.lower as string, b.upper as string)
}

function shift(v: string | number, by: number, type: RangeDataType) {
  return type === 'decimal' ? round((v as number) + by) : addDays(v as string, Math.round(by))
}

/**
 * Inserts a band after `index`, carving its span out of that band so coverage stays contiguous.
 * Finite bands split at their midpoint; open-ended bands give up one neighbour-width at the open end.
 */
export function insertBandAfter(bands: RangeBand[], index: number, type: RangeDataType, memberId: string): RangeBand[] {
  const next = bands.map((b) => ({ ...b }))
  const base = next[index]
  const fresh: RangeBand = { ordinal: 0, memberId, lower: null, upper: null, lowerInclusive: base?.lowerInclusive ?? true, upperInclusive: base?.upperInclusive ?? false }
  if (!base) return [...next, fresh]

  const neighbourWidth = widthOf(next[index - 1] ?? next[index + 1] ?? base, type) ?? (type === 'decimal' ? 1 : 30)
  const width = widthOf(base, type)

  // An unbounded band above a finite one: split the finite band's lower half instead of extrapolating past the data.
  const after = next[index + 1]
  const afterWidth = after ? widthOf(after, type) : null
  if (width === null && after && afterWidth !== null && afterWidth > 0 && !(type === 'date' && afterWidth < 2)) {
    const cut = shift(after.lower as string | number, afterWidth / 2, type)
    const closedLower = after.lowerInclusive
    fresh.lower = after.lower
    fresh.lowerInclusive = after.lowerInclusive
    fresh.upper = cut
    fresh.upperInclusive = !closedLower
    after.lower = cut
    after.lowerInclusive = closedLower
    next.splice(index + 1, 0, fresh)
    return next.map((b, i) => ({ ...b, ordinal: i + 1 }))
  }
  let cut: string | number | null = null

  if (width !== null) {
    if (width <= 0 || (type === 'date' && width < 2)) cut = null
    else cut = shift(base.lower as string | number, width / 2, type)
  } else if (isSet(base.lower)) cut = shift(base.lower, neighbourWidth, type)
  else if (isSet(base.upper)) cut = shift(base.upper, -neighbourWidth, type)

  if (cut !== null) {
    // Follow the band's own convention: `[a, b)` style keeps the cut in the upper half, `(a, b]` in the lower.
    const closedLower = isSet(base.lower) ? base.lowerInclusive : !base.upperInclusive
    fresh.lower = cut
    fresh.upper = base.upper
    fresh.upperInclusive = base.upperInclusive
    fresh.lowerInclusive = closedLower
    base.upper = cut
    base.upperInclusive = !closedLower
  } else {
    fresh.lower = base.upper
    fresh.lowerInclusive = !base.upperInclusive
  }
  next.splice(index + 1, 0, fresh)
  return next.map((b, i) => ({ ...b, ordinal: i + 1 }))
}

/** Applies a patch to band `index` and, when `linked`, moves the touching bound of each neighbour with it. */
export function patchBandLinked(bands: RangeBand[], index: number, patch: Partial<RangeBand>, linked: boolean): RangeBand[] {
  const next = bands.map((b, i) => (i === index ? { ...b, ...patch } : { ...b }))
  if (!linked) return next
  const cur = next[index]
  const prev = next[index - 1]
  const after = next[index + 1]
  if (prev && ('lower' in patch || 'lowerInclusive' in patch)) {
    prev.upper = cur.lower
    prev.upperInclusive = !cur.lowerInclusive
  }
  if (after && ('upper' in patch || 'upperInclusive' in patch)) {
    after.lower = cur.upper
    after.lowerInclusive = !cur.upperInclusive
  }
  return next
}

/**
 * Why a proposed bound for band `index` must be rejected, or null when it is acceptable.
 * Enforces lower < upper strictly; when `linked`, also keeps the neighbour that would be
 * dragged along from inverting (its own lower < upper).
 */
export function boundError(bands: RangeBand[], index: number, side: 'lower' | 'upper', value: Bound, linked: boolean, type: RangeDataType | null): string | null {
  if (!isSet(value)) return null
  const b = bands[index]
  if (!b) return null
  const v = value as string | number
  const fmt = (x: Bound) => formatBound(x, type)
  if (side === 'lower') {
    if (isSet(b.upper) && compareBound(v, b.upper as string | number) >= 0) return `Lower bound must be less than the upper bound (${fmt(b.upper)}).`
    const prev = bands[index - 1]
    if (linked && prev && isSet(prev.lower) && compareBound(v, prev.lower as string | number) <= 0)
      return `Must stay above band ${prev.ordinal}'s lower bound (${fmt(prev.lower)}) while bounds are linked.`
  } else {
    if (isSet(b.lower) && compareBound(v, b.lower as string | number) <= 0) return `Upper bound must be greater than the lower bound (${fmt(b.lower)}).`
    const after = bands[index + 1]
    if (linked && after && isSet(after.upper) && compareBound(v, after.upper as string | number) >= 0)
      return `Must stay below band ${after.ordinal}'s upper bound (${fmt(after.upper)}) while bounds are linked.`
  }
  return null
}

/** Shifts an ISO date (YYYY-MM-DD) by whole days; used for date-picker min/max. */
export function shiftIsoDate(iso: string, days: number) {
  const d = new Date(`${iso}T00:00:00Z`)
  if (Number.isNaN(d.getTime())) return undefined
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** Removes band `index`; when `linked`, a neighbour grows to cover the freed span. */
export function removeBandLinked(bands: RangeBand[], index: number, linked: boolean): RangeBand[] {
  const gone = bands[index]
  const next = bands.filter((_, i) => i !== index).map((b) => ({ ...b }))
  if (linked && gone) {
    const prev = next[index - 1]
    const after = next[index]
    if (prev) {
      prev.upper = gone.upper
      prev.upperInclusive = gone.upperInclusive
    } else if (after) {
      after.lower = gone.lower
      after.lowerInclusive = gone.lowerInclusive
    }
  }
  return next.map((b, i) => ({ ...b, ordinal: i + 1 }))
}

/* ───────────────────────── import ───────────────────────── */

export interface BandImportRow {
  memberKey: string
  memberName: string
  lower: Bound
  upper: Bound
  lowerInclusive: boolean
  upperInclusive: boolean
  error?: string
}

const BRACKETS: Record<string, [boolean, boolean]> = { '[)': [true, false], '(]': [false, true], '[]': [true, true], '()': [false, false] }

/** Parses CSV/TSV rows of `member key, name, lower, upper[, brackets]`. Empty bounds are unbounded. */
export function parseBandImport(text: string, type: RangeDataType): BandImportRow[] {
  return text
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .map((l) => l.split(l.includes('\t') ? '\t' : ',').map((c) => c.trim().replace(/^"|"$/g, '')))
    .filter((c, i) => !(i === 0 && /^(member\s*)?key$/i.test(c[0] ?? '')))
    .map(([memberKey = '', memberName = '', lo = '', hi = '', br = '']) => {
      const brackets = BRACKETS[br.replace(/\s/g, '') || '[)']
      const lower = parseBound(lo, type)
      const upper = parseBound(hi, type)
      let error: string | undefined
      if (!memberKey) error = 'Missing member key.'
      else if (lo.trim() && !isValidBound(lower, type)) error = `Lower bound '${lo}' is not a valid ${type === 'date' ? 'YYYY-MM-DD date' : 'number'}.`
      else if (hi.trim() && !isValidBound(upper, type)) error = `Upper bound '${hi}' is not a valid ${type === 'date' ? 'YYYY-MM-DD date' : 'number'}.`
      else if (!brackets) error = `Brackets '${br}' must be one of [) (] [] ().`
      return { memberKey, memberName: memberName || memberKey, lower, upper, lowerInclusive: brackets?.[0] ?? true, upperInclusive: brackets?.[1] ?? false, error }
    })
}

/** Distinct finite bounds in order, used to lay bands out on an ordinal scale. */
export function boundScale(bands: RangeBand[]) {
  const values: (string | number)[] = []
  for (const b of bands) for (const v of [b.lower, b.upper]) if (isSet(v) && !values.some((x) => compareBound(x, v) === 0)) values.push(v)
  return values.sort(compareBound)
}
