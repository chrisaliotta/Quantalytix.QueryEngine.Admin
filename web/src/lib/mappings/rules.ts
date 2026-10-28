/**
 * Presentational helpers for mapping sets and bridges: member lookup for labels, source key markers, paste parsing and
 * the paste preview's New / Unchanged / Changed / Removed grouping. Validity is decided by the server (OI-05); the
 * editors show the `violations` of the version content.
 */

import { newTempId, type BridgeMapping, type BridgeMappingInput, type MappingEntry, type MappingEntryInput, type MemberRef, type Violation } from './contract'

export type MemberIndex = Map<string, MemberRef>

export function indexMembers(members: MemberRef[] | null | undefined): MemberIndex | null {
  return members ? new Map(members.map((m) => [m.key, m])) : null
}

/** Weights and factors compare with a small tolerance so 0.1 + 0.2 equals 0.3. */
const EPS = 1e-9
export const nearlyEqual = (a: number, b: number) => Math.abs(a - b) < EPS
export const fmt = (n: number) => String(Math.round(n * 1e6) / 1e6)

/** Violations per item: by `target.itemId`, plus per-key findings under `target.itemKey`. */
export function violationsByTarget(violations: Violation[]) {
  const byId = new Map<string, Violation[]>()
  const byKey = new Map<string, Violation[]>()
  for (const v of violations) {
    if (v.target?.itemId) byId.set(v.target.itemId, [...(byId.get(v.target.itemId) ?? []), v])
    else if (v.target?.itemKey) byKey.set(v.target.itemKey, [...(byKey.get(v.target.itemKey) ?? []), v])
  }
  return { byId, byKey }
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

type ParsedEntry = Omit<MappingEntryInput, 'id'>
type ParsedBridgeRow = Omit<BridgeMappingInput, 'id' | 'sourceSelection'>

export function parseEntryPaste(text: string): ParsedLine<ParsedEntry>[] {
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

export function parseBridgePaste(text: string): ParsedLine<ParsedBridgeRow>[] {
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

/* ───────────────────────── paste previews ───────────────────────── */

export type PreviewStatus = 'new' | 'unchanged' | 'changed' | 'removed' | 'error'

export interface PastePreview<Row, Result> {
  rows: Row[]
  counts: Record<PreviewStatus, number>
  /** The full replacement list for `replaceEntries` / `replaceBridgeMappings`. */
  result: Result[]
}

function countRows(rows: { status: PreviewStatus }[]) {
  const counts: Record<PreviewStatus, number> = { new: 0, unchanged: 0, changed: 0, removed: 0, error: 0 }
  for (const r of rows) counts[r.status]++
  return counts
}

export interface EntryPreviewRow {
  key: string
  status: PreviewStatus
  sourceKeyValue: string
  memberKey: string
  previousMemberKey?: string
  error?: string
  line?: number
}

export function previewEntryPaste(existing: MappingEntry[], text: string, keepExisting: boolean): PastePreview<EntryPreviewRow, MappingEntryInput> {
  const current = new Map(existing.map((e) => [e.sourceKeyValue, e]))
  const pasted = new Map<string, number>()
  const rows: EntryPreviewRow[] = []
  const result: MappingEntryInput[] = []

  for (const p of parseEntryPaste(text)) {
    if (!p.value) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceKeyValue: p.raw, memberKey: '', error: p.error, line: p.line })
      continue
    }
    const { sourceKeyValue, memberKey } = p.value
    const firstLine = pasted.get(sourceKeyValue)
    if (firstLine !== undefined) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceKeyValue, memberKey, error: `Duplicate of line ${firstLine}`, line: p.line })
      continue
    }
    pasted.set(sourceKeyValue, p.line)
    const prev = current.get(sourceKeyValue)
    const status: PreviewStatus = !prev ? 'new' : prev.memberKey === memberKey ? 'unchanged' : 'changed'
    rows.push({ key: `l${p.line}`, status, sourceKeyValue, memberKey, previousMemberKey: status === 'changed' ? prev!.memberKey : undefined, line: p.line })
    result.push({ id: prev?.id ?? newTempId(), sourceKeyValue, memberKey })
  }
  for (const e of existing) {
    if (pasted.has(e.sourceKeyValue)) continue
    if (keepExisting) result.push({ id: e.id, sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey })
    else rows.push({ key: `r${e.id}`, status: 'removed', sourceKeyValue: e.sourceKeyValue, memberKey: e.memberKey })
  }
  return { rows, counts: countRows(rows), result }
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

export function previewBridgePaste(existing: BridgeMapping[], text: string, keepExisting: boolean): PastePreview<BridgePreviewRow, BridgeMappingInput> {
  const pairKey = (s: string, t: string) => `${s}\u0000${t}`
  const current = new Map(existing.map((m) => [pairKey(m.sourceMemberKey, m.targetMemberKey), m]))
  const pasted = new Map<string, number>()
  const rows: BridgePreviewRow[] = []
  const result: BridgeMappingInput[] = []

  for (const p of parseBridgePaste(text)) {
    if (!p.value) {
      rows.push({ key: `l${p.line}`, status: 'error', sourceMemberKey: p.raw, targetMemberKey: '', factor: 1, allocationWeight: 1, error: p.error, line: p.line })
      continue
    }
    const v = p.value
    const k = pairKey(v.sourceMemberKey, v.targetMemberKey)
    const firstLine = pasted.get(k)
    if (firstLine !== undefined) {
      rows.push({ key: `l${p.line}`, status: 'error', ...v, error: `Duplicate of line ${firstLine}`, line: p.line })
      continue
    }
    pasted.set(k, p.line)
    const prev = current.get(k)
    const same = prev && nearlyEqual(prev.factor, v.factor) && nearlyEqual(prev.allocationWeight, v.allocationWeight)
    const status: PreviewStatus = !prev ? 'new' : same ? 'unchanged' : 'changed'
    rows.push({ key: `l${p.line}`, status, ...v, previous: status === 'changed' ? `× ${fmt(prev!.factor)} · w ${fmt(prev!.allocationWeight)}` : undefined, line: p.line })
    result.push({ ...v, id: prev?.id ?? newTempId() })
  }
  for (const m of existing) {
    if (pasted.has(pairKey(m.sourceMemberKey, m.targetMemberKey))) continue
    if (keepExisting) result.push({ id: m.id, sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: m.factor, allocationWeight: m.allocationWeight })
    else rows.push({ key: `r${m.id}`, status: 'removed', sourceMemberKey: m.sourceMemberKey, targetMemberKey: m.targetMemberKey, factor: m.factor, allocationWeight: m.allocationWeight })
  }
  return { rows, counts: countRows(rows), result }
}
