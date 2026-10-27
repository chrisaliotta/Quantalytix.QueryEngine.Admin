/**
 * The mock's stand-in for the server's mapping and bridge version rules (blueprint 13, Rule codes). Only the mock
 * adapter runs them; screens read the resulting `violations`, `coverage` and `allocation` from the version content.
 */

import type {
  BridgeAllocationSummary,
  BridgeMapping,
  MappingAllocationMode,
  MappingCardinality,
  MappingCoverage,
  MappingEntry,
  MemberRef,
  Violation,
} from '../contract'
import { fmt, nearlyEqual, type MemberIndex } from '../rules'

const ENTRIES = 'entries'
const MAPPINGS = 'mappings'

export function mappingSetViolations(entries: MappingEntry[], members: MemberIndex | null, dimensionName: string): Violation[] {
  const out: Violation[] = []
  if (!members) {
    out.push({ code: 'dimension_version_published', message: `${dimensionName} has no live version, so member keys cannot be resolved.` })
  }
  const seen = new Map<string, MappingEntry>()
  for (const e of entries) {
    const first = seen.get(e.sourceKeyValue)
    if (first) {
      out.push({
        code: 'source_key_unique',
        message: `Source key '${e.sourceKeyValue}' appears more than once (→ ${first.memberKey} and → ${e.memberKey}).`,
        target: { area: ENTRIES, itemId: e.id, filter: e.sourceKeyValue },
      })
    } else seen.set(e.sourceKeyValue, e)
    if (!members) continue
    const m = members.get(e.memberKey)
    if (!m)
      out.push({
        code: 'entry_member_in_version',
        message: `'${e.sourceKeyValue}' maps to ${e.memberKey}, which is not a member of the live ${dimensionName} version.`,
        target: { area: ENTRIES, itemId: e.id, filter: e.memberKey },
      })
    else if (!m.isAssignable)
      out.push({
        code: 'entry_member_assignable',
        message: `'${e.sourceKeyValue}' maps to ${e.memberKey} (${m.displayName}), a folder that cannot receive source values.`,
        target: { area: ENTRIES, itemId: e.id, filter: e.memberKey },
      })
  }
  return out
}

export function mappingCoverage(entries: MappingEntry[], members: MemberRef[] | null): MappingCoverage {
  const assignable = (members ?? []).filter((m) => m.isAssignable)
  const used = new Set(entries.map((e) => e.memberKey))
  return { assignableMemberCount: assignable.length, uncoveredMemberKeys: assignable.filter((m) => !used.has(m.key)).map((m) => m.key) }
}

export function bridgeAllocation(mappings: BridgeMapping[]): BridgeAllocationSummary {
  const bySource = new Map<string, { sourceMemberKey: string; targetCount: number; weightSum: number }>()
  for (const m of mappings) {
    const s = bySource.get(m.sourceMemberKey) ?? { sourceMemberKey: m.sourceMemberKey, targetCount: 0, weightSum: 0 }
    s.targetCount++
    s.weightSum += m.allocationWeight
    bySource.set(m.sourceMemberKey, s)
  }
  return { sources: [...bySource.values()].sort((a, b) => a.sourceMemberKey.localeCompare(b.sourceMemberKey)) }
}

interface Endpoint {
  name: string
  members: MemberIndex | null
}

export function bridgeViolations(
  cardinality: MappingCardinality,
  mode: MappingAllocationMode,
  mappings: BridgeMapping[],
  source: Endpoint,
  target: Endpoint,
  isReportBridge: boolean,
): Violation[] {
  const out: Violation[] = []
  for (const end of [source, target]) {
    if (!end.members) out.push({ code: 'endpoint_versions_published', message: `${end.name} has no live version, so its member keys cannot be resolved.` })
  }
  const seen = new Set<string>()
  for (const m of mappings) {
    const at = { area: MAPPINGS, itemId: m.id, filter: m.sourceMemberKey }
    const pair = `${m.sourceMemberKey}\u0000${m.targetMemberKey}`
    if (seen.has(pair)) out.push({ code: 'pair_unique', message: `${m.sourceMemberKey} → ${m.targetMemberKey} appears more than once.`, target: at })
    seen.add(pair)
    if (source.members && !source.members.has(m.sourceMemberKey))
      out.push({ code: 'mapping_members_in_endpoint_versions', message: `Source ${m.sourceMemberKey} is not a member of the live ${source.name} version.`, target: at })
    if (target.members && !target.members.has(m.targetMemberKey))
      out.push({ code: 'mapping_members_in_endpoint_versions', message: `Target ${m.targetMemberKey} is not a member of the live ${target.name} version.`, target: { ...at, filter: m.targetMemberKey } })
    if (m.sourceSelection === 'descendants' && !isReportBridge)
      out.push({ code: 'descendants_report_only', message: `${m.sourceMemberKey} includes its descendants; only report bridges may.`, target: at })
    if (mode === 'none' && !nearlyEqual(m.allocationWeight, 1))
      out.push({ code: 'allocation_none', message: `${m.sourceMemberKey} → ${m.targetMemberKey} has weight ${fmt(m.allocationWeight)}; allocation none requires exactly 1.`, target: at })
    if (m.allocationWeight < 0)
      out.push({
        code: mode === 'replicate' ? 'allocation_replicate' : 'allocation_partition',
        message: `${m.sourceMemberKey} → ${m.targetMemberKey} has a negative weight (${fmt(m.allocationWeight)}).`,
        target: at,
      })
  }
  const singleTarget = cardinality === 'many_to_one' || mode === 'none'
  for (const s of bridgeAllocation(mappings).sources) {
    const at = { area: MAPPINGS, itemKey: s.sourceMemberKey, filter: s.sourceMemberKey }
    if (singleTarget && s.targetCount > 1)
      out.push({
        code: cardinality === 'many_to_one' ? 'cardinality_many_to_one' : 'allocation_none',
        message: `${s.sourceMemberKey} maps to ${s.targetCount} targets; ${cardinality === 'many_to_one' ? 'many-to-one' : 'allocation none'} allows one.`,
        target: at,
      })
    if (mode === 'partition' && !nearlyEqual(s.weightSum, 1))
      out.push({ code: 'allocation_partition', message: `${s.sourceMemberKey}'s weights sum to ${fmt(s.weightSum)}; partition requires exactly 1.`, target: at })
  }
  return out
}
