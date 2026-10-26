/**
 * Pure rules for mapping sets and bridges. The UI runs them for inline checks and paste previews;
 * the mock adapter runs the same functions when validating or publishing, so both always agree.
 */

import type { AllocationMode, BridgeMapping, BridgeMappingInput, Cardinality, EntryInput, MappingEntry, MemberRef, Violation } from './contract'

export type MemberIndex = Map<string, MemberRef>

export function indexMembers(members: MemberRef[] | null | undefined): MemberIndex | null {
  return members ? new Map(members.map((m) => [m.key, m])) : null
}

/** Weights and factors compare with a small tolerance so 0.1 + 0.2 sums to 0.3. */
const EPS = 1e-9
export const sumsToOne = (n: number) => Math.abs(n - 1) < EPS
export const fmt = (n: number) => String(Math.round(n * 1e6) / 1e6)

/* ───────────────────────── mapping set entries ───────────────────────── */

export type EntryIssue = { code: 'duplicate'; otherMemberKey: string } | { code: 'missing' } | { code: 'folder' }

export function entryIssues(entries: Pick<MappingEntry, 'id' | 'sourceKeyValue' | 'memberKey'>[], members: MemberIndex | null): Map<string, EntryIssue[]> {
  const out = new Map<string, EntryIssue[]>()
  const bySource = new Map<string, typeof entries>()
  for (const e of entries) bySource.set(e.sourceKeyValue, [...(bySource.get(e.sourceKeyValue) ?? []), e])
  for (const e of entries) {
    const issues: EntryIssue[] = []
    const other = bySource.get(e.sourceKeyValue)!.find((x) => x.id !== e.id)
    if (other) issues.push({ code: 'duplicate', otherMemberKey: other.memberKey })
    if (members) {
      const m = members.get(e.memberKey)
      if (!m) issues.push({ code: 'missing' })
      else if (!m.isAssignable) issues.push({ code: 'folder' })
    }
    if (issues.length) out.set(e.id, issues)
  }
  return out
}

export function entryIssueText(issue: EntryIssue, members: MemberIndex | null) {
  switch (issue.code) {
    case 'duplicate': {
      const label = members?.get(issue.otherMemberKey)?.displayName
      return `Already maps to ${issue.otherMemberKey}${label ? ` (${label})` : ''}`
    }
    case 'missing':
      return 'Not a member of the published version'
    case 'folder':
      return 'Folder — not assignable'
  }
}

export function mappingSetViolations(entries: MappingEntry[], members: MemberIndex | null, dimensionName: string): Violation[] {
  const out: Violation[] = []
  if (!members) {
    out.push({ code: 'dimension_version_published', message: `${dimensionName} has no published version, so member keys cannot be resolved.` })
  }
  const seen = new Map<string, MappingEntry>()
  for (const e of entries) {
    const first = seen.get(e.sourceKeyValue)
    if (first) {
      out.push({ code: 'source_key_unique', message: `Source key '${e.sourceKeyValue}' appears more than once (→ ${first.memberKey} and → ${e.memberKey}).`, itemId: e.id, filter: e.sourceKeyValue })
    } else seen.set(e.sourceKeyValue, e)
    if (!members) continue
    const m = members.get(e.memberKey)
    if (!m) out.push({ code: 'entry_member_in_version', message: `'${e.sourceKeyValue}' maps to ${e.memberKey}, which is not a member of the published ${dimensionName} version.`, itemId: e.id, filter: e.memberKey })
    else if (!m.isAssignable) out.push({ code: 'entry_member_assignable', message: `'${e.sourceKeyValue}' maps to ${e.memberKey} (${m.displayName}), a folder that cannot receive source values.`, itemId: e.id, filter: e.memberKey })
  }
  return out
}

/** Members no entry maps to: gaps a user may want to fill. */
export function uncoveredMembers(entries: Pick<MappingEntry, 'memberKey'>[], members: MemberRef[] | null) {
  if (!members) return []
  const used = new Set(entries.map((e) => e.memberKey))
  return members.filter((m) => m.isAssignable && !used.has(m.key))
}

/** Visible hints for characters that matter when comparing as text. */
export function sourceKeyMarkers(value: string) {
  const out: string[] = []
  if (/^\s/.test(value)) out.push('leading space')
  if (/\s$/.test(value)) out.push('trailing space')
  if (/^0\d/.test(value.trimStart())) out.push('leading zero')
  return out
}

/* ───────────────────────── paste parsing ───────────────────────── */

export interface ParsedLine<T> {
  line: number
  raw: string
  value: T | null
  error?: string
}

/** Splits on the first tab if present, else on commas. The source key is kept byte for byte. */
function splitLine(raw: string) {
  return raw.includes('\t') ? raw.split('\t') : raw.split(',')
}

function contentLines(text: string) {
  return text
    .split(/\r?\n/)
    .map((raw, i) => ({ raw, line: i + 1 }))
    .filter(({ raw }) => raw.trim() !== '' && !raw.trimStart().startsWith('#'))
}

export function parseEntryPaste(text: string): ParsedLine<EntryInput>[] {
  return contentLines(text).map(({ raw, line }) => {
    const parts = splitLine(raw)
    if (parts.length < 2) return { line, raw, value: null, error: 'Expected source_key,member_key' }
    const memberKey = parts.slice(1).join(',').trim()
    if (!memberKey) return { line, raw, value: null, error: 'Member key is empty' }
    return { line, raw, value: { sourceKeyValue: parts[0], memberKey } }
  })
}

export function entriesToText(entries: Pick<MappingEntry, 'sourceKeyValue' | 'memberKey'>[]) {
  return entries.map((e) => `${e.sourceKeyValue},${e.memberKey}`).join('\n')
}

export type PreviewStatus = 'new' | 'unchanged' | 'changed' | 'removed' | 'error'

export interface EntryPreviewRow {
  key: string
  status: PreviewStatus
  sourceKeyValue: string
  memberKey: string
  previousMemberKey?: string
  error?: string
  line?: number
}

export interface PastePreview<Row, Result> {
  rows: Row[]
  counts: Record<PreviewStatus, number>
  result: Result[]
}

const emptyCounts = (): Record<PreviewStatus, number> => ({ new: 0, unchanged: 0, changed: 0, removed: 0, error: 0 })

export function previewEntryPaste(existing: MappingEntry[], text: string, members: MemberIndex | null, keepExisting: boolean): PastePreview<EntryPreviewRow, EntryInput> {
  const parsed = parseEntryPaste(text)
  const current = new Map(existing.map((e) => [e.sourceKeyValue, e]))
  const pasted = new Map<string, number>()
  const rows: EntryPreviewRow[] = []
  const result: EntryInput[] = []

  for (const p of parsed) {
    if (!p.value) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceKeyValue: p.raw, memberKey: '', error: p.error, line: p.line })
      continue
    }
    const { sourceKeyValue, memberKey } = p.value
    let error: string | undefined
    if (pasted.has(sourceKeyValue)) error = `Duplicate of line ${pasted.get(sourceKeyValue)}`
    else if (members && !members.get(memberKey)) error = 'Unknown member'
    else if (members && !members.get(memberKey)!.isAssignable) error = 'Folder member'
    if (!pasted.has(sourceKeyValue)) pasted.set(sourceKeyValue, p.line)
    const prev = current.get(sourceKeyValue)
    if (error) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceKeyValue, memberKey, error, line: p.line })
      continue
    }
    const status: PreviewStatus = !prev ? 'new' : prev.memberKey === memberKey ? 'unchanged' : 'changed'
    rows.push({ key: `l${p.line}`, status, sourceKeyValue, memberKey, previousMemberKey: status === 'changed' ? prev!.memberKey : undefined, line: p.line })
    result.push({ id: prev?.id, sourceKeyValue, memberKey })
  }
  for (const e of existing) {
    if (pasted.has(e.sourceKeyValue)) continue
    if (keepExisting) result.push({ id: e.id, sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey })
    else rows.push({ key: `r${e.id}`, status: 'removed', sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey })
  }
  const counts = emptyCounts()
  for (const r of rows) counts[r.status]++
  return { rows, counts, result }
}

/* ───────────────────────── bridges ───────────────────────── */

type MappingLike = Pick<BridgeMapping, 'sourceMemberKey' | 'targetMemberKey' | 'allocationWeight'> & { id?: string }

export interface SourceSummary {
  sourceKey: string
  targets: string[]
  weightSum: number
}

export function summarizeSources(mappings: MappingLike[]): Map<string, SourceSummary> {
  const out = new Map<string, SourceSummary>()
  for (const m of mappings) {
    const s = out.get(m.sourceMemberKey) ?? { sourceKey: m.sourceMemberKey, targets: [], weightSum: 0 }
    s.targets.push(m.targetMemberKey)
    s.weightSum += m.allocationWeight
    out.set(m.sourceMemberKey, s)
  }
  return out
}

export interface AllocationSummary {
  /** Sources with more than one target, when the settings forbid it. */
  multiTarget: SourceSummary[]
  /** Partition only: sources whose weights do not sum to 1. */
  badSums: SourceSummary[]
  /** None only: mappings with a weight other than 1. */
  weightNotOne: number
  negative: number
}

export function allocationSummary(cardinality: Cardinality, mode: AllocationMode, mappings: MappingLike[]): AllocationSummary {
  const sources = [...summarizeSources(mappings).values()]
  const singleTarget = cardinality === 'many_to_one' || mode === 'none'
  return {
    multiTarget: singleTarget ? sources.filter((s) => s.targets.length > 1) : [],
    badSums: mode === 'partition' ? sources.filter((s) => !sumsToOne(s.weightSum)) : [],
    weightNotOne: mode === 'none' ? mappings.filter((m) => !sumsToOne(m.allocationWeight)).length : 0,
    negative: mappings.filter((m) => m.allocationWeight < 0).length,
  }
}

export function allocationIssueCount(s: AllocationSummary) {
  return s.multiTarget.length + s.badSums.length + s.weightNotOne + s.negative
}

export type BridgeIssue =
  | { code: 'pair' }
  | { code: 'missingSource' }
  | { code: 'missingTarget' }
  | { code: 'secondTarget' }
  | { code: 'weightNotOne' }
  | { code: 'negativeWeight' }
  | { code: 'badSum'; sum: number }

export const BRIDGE_ISSUE_TEXT: Record<BridgeIssue['code'], string> = {
  pair: 'This source → target pair already exists',
  missingSource: 'Source is not a member of the published version',
  missingTarget: 'Target is not a member of the published version',
  secondTarget: 'Second target for this source — not allowed under the current settings',
  weightNotOne: 'Weight must be exactly 1 under allocation none',
  negativeWeight: 'Weight cannot be negative',
  badSum: "This source's weights do not sum to 1",
}

export function bridgeIssues(
  cardinality: Cardinality,
  mode: AllocationMode,
  mappings: (MappingLike & { id: string })[],
  sourceMembers: MemberIndex | null,
  targetMembers: MemberIndex | null,
): Map<string, BridgeIssue[]> {
  const out = new Map<string, BridgeIssue[]>()
  const sums = summarizeSources(mappings)
  const singleTarget = cardinality === 'many_to_one' || mode === 'none'
  const seenPairs = new Set<string>()
  const firstTarget = new Map<string, string>()
  for (const m of mappings) {
    const issues: BridgeIssue[] = []
    const pair = `${m.sourceMemberKey}\u0000${m.targetMemberKey}`
    if (seenPairs.has(pair)) issues.push({ code: 'pair' })
    seenPairs.add(pair)
    if (sourceMembers && !sourceMembers.has(m.sourceMemberKey)) issues.push({ code: 'missingSource' })
    if (targetMembers && !targetMembers.has(m.targetMemberKey)) issues.push({ code: 'missingTarget' })
    const first = firstTarget.get(m.sourceMemberKey)
    if (first === undefined) firstTarget.set(m.sourceMemberKey, m.targetMemberKey)
    else if (singleTarget && first !== m.targetMemberKey) issues.push({ code: 'secondTarget' })
    if (mode === 'none' && !sumsToOne(m.allocationWeight)) issues.push({ code: 'weightNotOne' })
    if (m.allocationWeight < 0) issues.push({ code: 'negativeWeight' })
    const s = sums.get(m.sourceMemberKey)!
    if (mode === 'partition' && !sumsToOne(s.weightSum)) issues.push({ code: 'badSum', sum: s.weightSum })
    if (issues.length) out.set(m.id, issues)
  }
  return out
}

export function bridgeViolations(
  cardinality: Cardinality,
  mode: AllocationMode,
  mappings: BridgeMapping[],
  source: { name: string; members: MemberIndex | null },
  target: { name: string; members: MemberIndex | null },
): Violation[] {
  const out: Violation[] = []
  for (const end of [source, target]) {
    if (!end.members) out.push({ code: 'endpoint_versions_published', message: `${end.name} has no published version, so its member keys cannot be resolved.` })
  }
  const seen = new Set<string>()
  for (const m of mappings) {
    const pair = `${m.sourceMemberKey}\u0000${m.targetMemberKey}`
    if (seen.has(pair)) out.push({ code: 'pair_unique', message: `${m.sourceMemberKey} → ${m.targetMemberKey} appears more than once.`, itemId: m.id, filter: m.sourceMemberKey })
    seen.add(pair)
    if (source.members && !source.members.has(m.sourceMemberKey))
      out.push({ code: 'mapping_members_in_endpoint_versions', message: `Source ${m.sourceMemberKey} is not a member of the published ${source.name} version.`, itemId: m.id, filter: m.sourceMemberKey })
    if (target.members && !target.members.has(m.targetMemberKey))
      out.push({ code: 'mapping_members_in_endpoint_versions', message: `Target ${m.targetMemberKey} is not a member of the published ${target.name} version.`, itemId: m.id, filter: m.targetMemberKey })
  }
  const summary = allocationSummary(cardinality, mode, mappings)
  for (const s of summary.multiTarget) {
    out.push({
      code: cardinality === 'many_to_one' ? 'cardinality_many_to_one' : 'allocation_none',
      message: `${s.sourceKey} maps to ${s.targets.length} targets (${s.targets.join(', ')}); ${cardinality === 'many_to_one' ? 'many-to-one allows one' : 'allocation none allows one'}.`,
      filter: s.sourceKey,
    })
  }
  if (mode === 'none') {
    for (const m of mappings.filter((x) => !sumsToOne(x.allocationWeight)))
      out.push({ code: 'allocation_none', message: `${m.sourceMemberKey} → ${m.targetMemberKey} has weight ${fmt(m.allocationWeight)}; allocation none requires exactly 1.`, itemId: m.id, filter: m.sourceMemberKey })
  }
  for (const s of summary.badSums) out.push({ code: 'allocation_partition', message: `${s.sourceKey}'s weights sum to ${fmt(s.weightSum)}; partition requires exactly 1.`, filter: s.sourceKey })
  for (const m of mappings.filter((x) => x.allocationWeight < 0))
    out.push({
      code: mode === 'replicate' ? 'allocation_replicate' : 'allocation_partition',
      message: `${m.sourceMemberKey} → ${m.targetMemberKey} has a negative weight (${fmt(m.allocationWeight)}).`,
      itemId: m.id,
      filter: m.sourceMemberKey,
    })
  return out
}

/** Mappings that break the rules of a different setting; used to confirm a settings switch. */
export function nonCompliantCount(cardinality: Cardinality, mode: AllocationMode, mappings: (MappingLike & { id: string })[]) {
  return bridgeIssues(cardinality, mode, mappings, null, null).size
}

export function parseBridgePaste(text: string): ParsedLine<BridgeMappingInput>[] {
  return contentLines(text).map(({ raw, line }) => {
    const parts = splitLine(raw).map((p) => p.trim())
    if (parts.length < 2 || !parts[0] || !parts[1]) return { line, raw, value: null, error: 'Expected source_key,target_key[,factor[,weight]]' }
    const factor = parts[2] ? Number(parts[2]) : 1
    const weight = parts[3] ? Number(parts[3]) : 1
    if (!Number.isFinite(factor)) return { line, raw, value: null, error: `Factor '${parts[2]}' is not a number` }
    if (!Number.isFinite(weight)) return { line, raw, value: null, error: `Weight '${parts[3]}' is not a number` }
    return { line, raw, value: { sourceMemberKey: parts[0], targetMemberKey: parts[1], factor, allocationWeight: weight } }
  })
}

export function bridgeMappingsToText(mappings: Pick<BridgeMapping, 'sourceMemberKey' | 'targetMemberKey' | 'factor' | 'allocationWeight'>[]) {
  return mappings.map((m) => `${m.sourceMemberKey},${m.targetMemberKey},${fmt(m.factor)},${fmt(m.allocationWeight)}`).join('\n')
}

export interface BridgePreviewRow {
  key: string
  status: PreviewStatus
  sourceMemberKey: string
  targetMemberKey: string
  factor: number
  allocationWeight: number
  previous?: string
  error?: string
  line?: number
}

export function previewBridgePaste(
  existing: BridgeMapping[],
  text: string,
  keepExisting: boolean,
  sourceMembers: MemberIndex | null,
  targetMembers: MemberIndex | null,
): PastePreview<BridgePreviewRow, BridgeMappingInput> {
  const parsed = parseBridgePaste(text)
  const pairKey = (s: string, t: string) => `${s}\u0000${t}`
  const current = new Map(existing.map((m) => [pairKey(m.sourceMemberKey, m.targetMemberKey), m]))
  const pasted = new Map<string, number>()
  const rows: BridgePreviewRow[] = []
  const result: BridgeMappingInput[] = []
  for (const p of parsed) {
    if (!p.value) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceMemberKey: p.raw, targetMemberKey: '', factor: 1, allocationWeight: 1, error: p.error, line: p.line })
      continue
    }
    const v = p.value
    const k = pairKey(v.sourceMemberKey, v.targetMemberKey)
    let error: string | undefined
    if (pasted.has(k)) error = `Duplicate of line ${pasted.get(k)}`
    else if (sourceMembers && !sourceMembers.has(v.sourceMemberKey)) error = 'Unknown source member'
    else if (targetMembers && !targetMembers.has(v.targetMemberKey)) error = 'Unknown target member'
    else if (v.allocationWeight < 0) error = 'Negative weight'
    if (!pasted.has(k)) pasted.set(k, p.line)
    if (error) {
      rows.push({ key: `l${p.line}`, status: 'error', ...v, error, line: p.line })
      continue
    }
    const prev = current.get(k)
    const same = prev && sumsToOne(prev.factor - v.factor + 1) && sumsToOne(prev.allocationWeight - v.allocationWeight + 1)
    const status: PreviewStatus = !prev ? 'new' : same ? 'unchanged' : 'changed'
    rows.push({ key: `l${p.line}`, status, ...v, previous: status === 'changed' ? `× ${fmt(prev!.factor)} · w ${fmt(prev!.allocationWeight)}` : undefined, line: p.line })
    result.push({ ...v, id: prev?.id, sourceSelection: prev?.sourceSelection ?? 'member' })
  }
  for (const m of existing) {
    if (pasted.has(pairKey(m.sourceMemberKey, m.targetMemberKey))) continue
    if (keepExisting) result.push({ id: m.id, sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: m.factor, allocationWeight: m.allocationWeight, sourceSelection: m.sourceSelection })
    else rows.push({ key: `r${m.id}`, status: 'removed', sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: m.factor, allocationWeight: m.allocationWeight })
  }
  const counts = emptyCounts()
  for (const r of rows) counts[r.status]++
  return { rows, counts, result }
}

/** Keys under a member in its tree (excluding itself), in tree order; used to show what a descendants rule covers. */
export function descendantKeys(rootKey: string, members: MemberRef[]): string[] {
  const children = new Map<string, MemberRef[]>()
  for (const m of members) if (m.parentKey) children.set(m.parentKey, [...(children.get(m.parentKey) ?? []), m])
  const out: string[] = []
  const walk = (k: string) => {
    for (const c of children.get(k) ?? []) {
      out.push(c.key)
      walk(c.key)
    }
  }
  walk(rootKey)
  return out
}
