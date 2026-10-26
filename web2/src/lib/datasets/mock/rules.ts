import type { DatasetTab, Dependent, PublicationViolation, SourceColumn, TenantCatalog, Periodicity } from '../contract'
import type { StoredVersion } from './data'

const PERIOD_RANK: Record<Periodicity, number> = { day: 0, month: 1, quarter: 2, year: 3 }

export const FIELD_REF = /\[f\.([A-Za-z0-9_]+)\]/g
export const MEASURE_REF = /\[m\.([A-Za-z0-9_]+)\]/g

export function refsOf(text: string | null | undefined, pattern: RegExp): string[] {
  if (!text) return []
  return [...text.matchAll(new RegExp(pattern.source, 'g'))].map((m) => m[1])
}

function duplicates<T>(items: T[], key: (x: T) => string) {
  const seen = new Map<string, number>()
  items.forEach((x) => seen.set(key(x), (seen.get(key(x)) ?? 0) + 1))
  return items.filter((x) => (seen.get(key(x)) ?? 0) > 1)
}

/** Every publication rule over one version. Runs on validate and publish. */
export function publicationViolations(v: StoredVersion, catalog: TenantCatalog, columnsOf: (name: string) => SourceColumn[] | null): PublicationViolation[] {
  const out: PublicationViolation[] = []
  const push = (code: string, tab: DatasetTab, itemId: string | null, itemKey: string | null, message: string) => out.push({ code, tab, itemId, itemKey, message })

  const fieldById = new Map(v.fields.map((f) => [f.id, f]))
  const fieldByKey = new Map(v.fields.map((f) => [f.fieldKey, f]))
  const bindingById = new Map(v.bindings.map((b) => [b.id, b]))
  const grainById = new Map(v.grains.map((g) => [g.id, g]))
  const measureByKey = new Map(v.measures.map((m) => [m.measureKey, m]))
  const dim = (id: string | null) => catalog.dimensions.find((d) => d.id === id)
  const role = (id: string | null) => catalog.roles.find((r) => r.id === id)
  const timeBindings = v.bindings.filter((b) => b.bindingKind === 'time')
  const timeFieldIds = new Set(timeBindings.map((b) => b.sourceFieldId).filter(Boolean) as string[])

  /* Settings */
  const columns = columnsOf(v.settings.sourceObject)
  const kindOk = columns !== null
  if (!kindOk) push('source_object_exists', 'settings', null, null, `${v.settings.sourceObjectKind} '${v.settings.sourceObject}' does not exist in the tenant database.`)
  if (!v.settings.supportsAggregate && !v.settings.supportsDetail) push('version_supports_a_mode', 'settings', null, null, 'The version must support aggregate queries, detail queries, or both.')
  if (v.settings.defaultTimeBindingId && bindingById.get(v.settings.defaultTimeBindingId)?.bindingKind !== 'time')
    push('default_time_binding_in_version', 'settings', null, null, 'The default time binding must be a time binding of this version.')

  /* Fields */
  duplicates(v.fields, (f) => f.fieldKey).forEach((f) => push('field_key_unique', 'fields', f.id, f.fieldKey, `Field key '${f.fieldKey}' is used by more than one field.`))
  const colNames = new Set((columns ?? []).map((c) => c.name))
  for (const f of v.fields) {
    if (columns) {
      if (f.sourceColumn && f.fieldSourceKind !== 'expression' && !colNames.has(f.sourceColumn))
        push('field_columns_exist', 'fields', f.id, f.fieldKey, `Column '${f.sourceColumn}' is not on ${v.settings.sourceObject}.`)
    }
    if (f.fieldSourceKind === 'expression') {
      for (const key of refsOf(f.expressionText, FIELD_REF))
        if (!fieldByKey.has(key)) push('expression_field_references', 'fields', f.id, f.fieldKey, `[f.${key}] is not a field of this version.`)
    }
  }
  // Cycles through expression fields.
  const visiting = new Set<string>()
  const done = new Set<string>()
  const cyclic = new Set<string>()
  const walk = (key: string, stack: string[]) => {
    const f = fieldByKey.get(key)
    if (!f || f.fieldSourceKind !== 'expression' || done.has(key)) return
    if (visiting.has(key)) {
      stack.slice(stack.indexOf(key)).forEach((k) => cyclic.add(k))
      return
    }
    visiting.add(key)
    refsOf(f.expressionText, FIELD_REF).forEach((r) => walk(r, [...stack, key]))
    visiting.delete(key)
    done.add(key)
  }
  v.fields.forEach((f) => walk(f.fieldKey, []))
  cyclic.forEach((k) => {
    const f = fieldByKey.get(k)!
    push('expression_field_acyclic', 'fields', f.id, f.fieldKey, `[f.${k}] reads itself through other expression fields.`)
  })

  /* Grains */
  duplicates(v.grains, (g) => g.grainKey).forEach((g) => push('grain_key_unique', 'grains', g.id, g.grainKey, `Grain key '${g.grainKey}' is used by more than one grain.`))
  for (const g of v.grains) {
    if (g.fieldIds.some((id) => !fieldById.has(id))) push('grain_fields_in_version', 'grains', g.id, g.grainKey, 'Every grain component must be a field of this version.')
    if (g.grainKind === 'series')
      g.fieldIds
        .filter((id) => timeFieldIds.has(id))
        .forEach((id) => push('series_grain_excludes_time_field', 'grains', g.id, g.grainKey, `A series grain must not include the time field '${fieldById.get(id)?.fieldKey}'.`))
  }

  /* Bindings */
  const boundRoles = new Map<string, number>()
  v.bindings.forEach((b) => b.dimensionRoleId && boundRoles.set(b.dimensionRoleId, (boundRoles.get(b.dimensionRoleId) ?? 0) + 1))
  for (const b of v.bindings) {
    const r = role(b.dimensionRoleId)
    const key = b.bindingKind === 'time' ? (fieldById.get(b.sourceFieldId ?? '')?.fieldKey ?? 'time') : (r?.roleKey ?? b.id)
    const d = b.bindingKind === 'time' ? dim(b.dimensionId) : dim(r?.dimensionId ?? null)
    if (r && (boundRoles.get(r.id) ?? 0) > 1) push('bound_roles_distinct', 'bindings', b.id, key, `Role '${r.roleKey}' is bound more than once.`)
    if (r && fieldByKey.has(r.roleKey)) push('bound_roles_distinct', 'bindings', b.id, key, `Role key '${r.roleKey}' equals the key of a field in this version.`)
    if (b.sourceFieldId && !fieldById.has(b.sourceFieldId)) push('binding_fields_in_version', 'bindings', b.id, key, 'The source field is not a field of this version.')
    if (b.time?.intervalEndFieldId && !fieldById.has(b.time.intervalEndFieldId)) push('binding_fields_in_version', 'bindings', b.id, key, 'The interval end field is not a field of this version.')
    if (b.isGroupable && b.sourceFieldId && fieldById.get(b.sourceFieldId) && !fieldById.get(b.sourceFieldId)!.isGroupable && b.bindingKind !== 'time')
      push('binding_source_field_groupable', 'bindings', b.id, key, `A groupable binding needs a groupable source field; '${fieldById.get(b.sourceFieldId)!.fieldKey}' is not.`)
    if (b.bindingKind === 'mapped') {
      const ms = catalog.mappingSets.find((m) => m.id === b.mappingSetId)
      if (!ms || ms.dimensionId !== r?.dimensionId) push('mapped_binding_mapping_set_dimension', 'bindings', b.id, key, "The mapping set must belong to the role's dimension.")
    }
    if (b.bindingKind === 'range' && d?.dimensionKind !== 'range') push('range_binding_dimension_kind', 'bindings', b.id, key, 'A range binding needs a role of a range-kind dimension.')
    if (b.bindingKind === 'time' && d?.dimensionKind !== 'time') push('time_binding_dimension_kind', 'bindings', b.id, key, 'A time binding needs a time-kind dimension.')
    if (b.bindingKind === 'bridge') {
      const src = bindingById.get(b.sourceDimensionBindingId ?? '')
      const srcDim = src ? (catalog.roles.find((x) => x.id === src.dimensionRoleId)?.dimensionId ?? null) : null
      const br = catalog.bridges.find((x) => x.id === b.bridgeId)
      let loops = false
      const seen = new Set<string>([b.id])
      let cur = src
      while (cur?.bindingKind === 'bridge') {
        if (seen.has(cur.id)) {
          loops = true
          break
        }
        seen.add(cur.id)
        cur = bindingById.get(cur.sourceDimensionBindingId ?? '')
      }
      if (!src || !br || br.fromDimensionId !== srcDim || br.toDimensionId !== r?.dimensionId || loops)
        push('bridge_binding_route', 'bindings', b.id, key, loops ? 'The chain of source bindings loops.' : "The bridge must lead from the source binding's dimension into the role's dimension.")
    }
    if (b.unmappedBehavior === 'unknown' && b.unknownMemberKey && d && !d.publishedMembers.some((m) => m.key === b.unknownMemberKey))
      push('unknown_members_in_version', 'bindings', b.id, key, `'${b.unknownMemberKey}' is not a member of ${d.displayName}'s published version.`)
    if (b.bindingKind === 'time' && b.time) {
      const native = b.time.nativePeriodicity
      const finer = native ? b.time.allowedPeriodicities.filter((p) => PERIOD_RANK[p] < PERIOD_RANK[native]) : []
      if (finer.length) {
        const offending = v.measures.filter(
          (m) => m.isEnabled && m.measureKind !== 'formula' && (m.timePolicy?.requiredTimeBindingId ?? v.settings.defaultTimeBindingId) === b.id && m.timePolicy?.partialIntervalBehavior !== 'uniform_estimate',
        )
        if (offending.length)
          push(
            'interval_estimates_for_finer_periods',
            'bindings',
            b.id,
            key,
            `${finer.join(', ')} is finer than native ${native}; ${offending.map((m) => m.measureKey).join(', ')} must use uniform_estimate.`,
          )
      }
    }
  }

  /* Measures */
  duplicates(v.measures, (m) => m.measureKey).forEach((m) => push('measure_key_unique', 'measures', m.id, m.measureKey, `Measure key '${m.measureKey}' is used by more than one measure.`))
  for (const m of v.measures) {
    if (m.measureKind === 'formula') {
      for (const ref of refsOf(m.expressionText, MEASURE_REF)) if (!measureByKey.has(ref)) push('formula_references_known_measures', 'measures', m.id, m.measureKey, `[m.${ref}] is not a measure of this version.`)
    }
    if (m.components.some((cp) => !fieldById.has(cp.fieldId))) push('component_fields_in_version', 'measures', m.id, m.measureKey, 'Every component must be a field of this version.')
    if (m.measureKind === 'period_average' && !m.timePolicy) push('component_roles_match_kind', 'measures', m.id, m.measureKey, 'A period average always has a time policy.')
    const tp = m.timePolicy
    if (tp) {
      if (tp.requiredTimeBindingId && bindingById.get(tp.requiredTimeBindingId)?.bindingKind !== 'time')
        push('time_policy_time_binding_in_version', 'measures', m.id, m.measureKey, 'The required time binding must be a time binding of this version.')
      if (tp.observationSelection !== 'all') {
        const g = grainById.get(tp.selectionDatasetGrainId ?? '')
        if (!g || g.grainKind !== 'series') push('time_policy_series_grain', 'measures', m.id, m.measureKey, `'${tp.observationSelection}' selects per series; choose a series grain of this version.`)
      }
    }
  }
  // Formula cycles.
  const mVisiting = new Set<string>()
  const mDone = new Set<string>()
  const mCyclic = new Set<string>()
  const mWalk = (key: string, stack: string[]) => {
    const m = measureByKey.get(key)
    if (!m || m.measureKind !== 'formula' || mDone.has(key)) return
    if (mVisiting.has(key)) {
      stack.slice(stack.indexOf(key)).forEach((k) => mCyclic.add(k))
      return
    }
    mVisiting.add(key)
    refsOf(m.expressionText, MEASURE_REF).forEach((r) => mWalk(r, [...stack, key]))
    mVisiting.delete(key)
    mDone.add(key)
  }
  v.measures.forEach((m) => mWalk(m.measureKey, []))
  mCyclic.forEach((k) => {
    const m = measureByKey.get(k)!
    push('formula_acyclic', 'measures', m.id, m.measureKey, `[m.${k}] depends on itself through other formulas.`)
  })

  return out
}

/** Item id → everything that reads it. */
export function dependentsOf(v: StoredVersion, catalog: TenantCatalog): Record<string, Dependent[]> {
  const out: Record<string, Dependent[]> = {}
  const add = (targetId: string | null | undefined, dep: Dependent) => {
    if (!targetId) return
    ;(out[targetId] ??= []).push(dep)
  }
  const fieldByKey = new Map(v.fields.map((f) => [f.fieldKey, f]))
  const measureByKey = new Map(v.measures.map((m) => [m.measureKey, m]))
  const bindingKey = (b: StoredVersion['bindings'][number]) =>
    b.bindingKind === 'time' ? `time · ${v.fields.find((f) => f.id === b.sourceFieldId)?.fieldKey ?? '—'}` : (catalog.roles.find((r) => r.id === b.dimensionRoleId)?.roleKey ?? 'binding')

  for (const f of v.fields)
    for (const key of refsOf(f.expressionText, FIELD_REF)) add(fieldByKey.get(key)?.id, { tab: 'fields', itemId: f.id, itemKey: f.fieldKey, relation: 'expression', blocksDelete: false })

  for (const g of v.grains) g.fieldIds.forEach((id) => add(id, { tab: 'grains', itemId: g.id, itemKey: g.grainKey, relation: `${g.grainKind} grain component`, blocksDelete: true }))

  for (const b of v.bindings) {
    const key = bindingKey(b)
    add(b.sourceFieldId, { tab: 'bindings', itemId: b.id, itemKey: key, relation: 'binding source field', blocksDelete: true })
    add(b.time?.intervalEndFieldId, { tab: 'bindings', itemId: b.id, itemKey: key, relation: 'interval end field', blocksDelete: true })
    add(b.sourceDimensionBindingId, { tab: 'bindings', itemId: b.id, itemKey: key, relation: 'bridge source binding', blocksDelete: true })
  }

  if (v.settings.defaultTimeBindingId) add(v.settings.defaultTimeBindingId, { tab: 'settings', itemId: null, itemKey: 'Settings', relation: 'default time binding', blocksDelete: true })

  for (const m of v.measures) {
    m.components.forEach((cp) => add(cp.fieldId, { tab: 'measures', itemId: m.id, itemKey: m.measureKey, relation: `${cp.role} component`, blocksDelete: true }))
    for (const key of refsOf(m.rowFilterExpression, FIELD_REF)) add(fieldByKey.get(key)?.id, { tab: 'measures', itemId: m.id, itemKey: m.measureKey, relation: 'row filter', blocksDelete: false })
    for (const key of refsOf(m.expressionText, MEASURE_REF)) add(measureByKey.get(key)?.id, { tab: 'measures', itemId: m.id, itemKey: m.measureKey, relation: 'formula', blocksDelete: false })
    add(m.timePolicy?.requiredTimeBindingId, { tab: 'measures', itemId: m.id, itemKey: m.measureKey, relation: 'required time binding', blocksDelete: true })
    add(m.timePolicy?.selectionDatasetGrainId, { tab: 'measures', itemId: m.id, itemKey: m.measureKey, relation: 'selects by series grain', blocksDelete: true })
  }
  return out
}
