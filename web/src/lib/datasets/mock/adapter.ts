import type {
  Dataset,
  DatasetBinding,
  DatasetBindingInput,
  DatasetCreateInput,
  DatasetField,
  DatasetFieldInput,
  DatasetGrain,
  DatasetGrainInput,
  DatasetListItem,
  DatasetMeasure,
  DatasetMeasureInput,
  DatasetNewColumnInput,
  DatasetsApi,
  DatasetSourceColumn,
  DatasetSourceObject,
  DatasetSourceObjectKind,
  DatasetVersion,
  DatasetVersionSettings,
  DatasetVersionSummary,
  DeleteBlocker,
  DeleteCheck,
  Problem,
} from '../contract'
import type { Vocabularies } from '../lookups'
import { createSnapshotChangeSet, refill } from '@/lib/api/changes'
import { managedTablesDroppedWith } from '../managed-tables'
import { DOTTED_KEY_PATTERN, KEY_PATTERN } from '@/lib/api/contract'
import { LIVE_SNAPSHOT_ID, seedSnapshots } from '@/lib/snapshots/mock/data'
import { catalog, seedDatasets, seedVersions, sourceColumns as seedColumns, sourceObjects, vocabularies, type StoredVersion } from './data'
import { commitViolations, composesMeasures, dependentsOf, FIELD_REF, MEASURE_REF, refsOf } from './rules'

const JSON_PATH = /^\$(\.[A-Za-z_][A-Za-z0-9_]*|\[\d+\])+$/
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/
const TIME_TYPES = new Set(['date', 'datetime', 'timestamp'])

type Errors = Record<string, string[]>

function problem(status: Problem['status'], title: string, errors: Errors = {}, extra: Partial<Problem> = {}): { ok: false; problem: Problem } {
  return { ok: false, problem: { type: `urn:qe:problem:mock-${status}`, title, status, errors, ...extra } }
}
const ok = <T>(data: T): { ok: true; data: T } => ({ ok: true, data: structuredClone(data) })
const delay = () => new Promise((r) => setTimeout(r, 90 + Math.random() * 120))
const newId = (prefix: string) => `${prefix}_${Math.random().toString(36).slice(2, 9)}`

function err(errors: Errors, path: string, message: string) {
  ;(errors[path] ??= []).push(message)
}

function inVocab<K extends keyof Vocabularies>(vocab: K, value: unknown): boolean {
  const terms = vocabularies[vocab]
  return Array.isArray(terms) && terms.some((t) => t.value === value)
}

function checkVocab(errors: Errors, path: string, vocab: keyof Vocabularies, value: unknown, nullable = false) {
  if (value === null && nullable) return
  if (!inVocab(vocab, value)) err(errors, path, `'${String(value)}' is not in the ${vocab} vocabulary.`)
}

function checkBrackets(errors: Errors, path: string, text: string) {
  let depth = 0
  for (const ch of text) {
    if (ch === '[') depth++
    if (ch === ']') depth--
    if (depth < 0 || depth > 1) return err(errors, path, 'The expression has unbalanced brackets.')
  }
  if (depth !== 0) err(errors, path, 'The expression has unbalanced brackets.')
  const quotes = (text.match(/'/g) ?? []).length
  if (quotes % 2) err(errors, path, 'The expression has an unterminated string.')
}

function checkPrecision(errors: Errors, prefix: string, precision: number | null, scale: number | null) {
  if (precision === null || !Number.isInteger(precision) || precision < 1 || precision > 38) err(errors, `${prefix}Precision`, 'Precision must be a whole number from 1 to 38.')
  if (scale === null || !Number.isInteger(scale) || scale < 0) err(errors, `${prefix}Scale`, 'Scale must be a whole number of 0 or more.')
  else if (precision !== null && scale > precision) err(errors, `${prefix}Scale`, 'Scale cannot exceed precision.')
}

function parsesAsJson(text: string | null) {
  if (text === null || text.trim() === '') return true
  try {
    JSON.parse(text)
    return true
  } catch {
    return false
  }
}

const camel = (s: string) => s.replace(/_([a-z0-9])/g, (_, ch: string) => ch.toUpperCase())
const title = (s: string) => s.replace(/_/g, ' ').replace(/^./, (ch) => ch.toUpperCase())
const COLUMN_NAME = /^[a-z][a-z0-9_]{0,62}$/
const MATERIALIZE_MS = 2400

function nativeTypeOf(c: Pick<DatasetNewColumnInput, 'dataType' | 'numericPrecision' | 'numericScale'>) {
  const map: Record<string, string> = { boolean: 'boolean', int32: 'integer', int64: 'bigint', float: 'real', double: 'double precision', string: 'text', date: 'date', datetime: 'timestamp', timestamp: 'timestamptz', json: 'jsonb' }
  return c.dataType === 'decimal' ? `numeric(${c.numericPrecision ?? 18},${c.numericScale ?? 2})` : (map[c.dataType] ?? 'text')
}

function columnFromInput(c: DatasetNewColumnInput, generated = false): DatasetSourceColumn {
  return {
    name: c.name,
    nativeType: nativeTypeOf(c),
    mappedType: c.dataType,
    numericPrecision: c.dataType === 'decimal' ? c.numericPrecision : null,
    numericScale: c.dataType === 'decimal' ? c.numericScale : null,
    isNullable: c.isNullable,
    isGenerated: generated,
  }
}

const sameSource = (a: Pick<DatasetField, 'sourceColumn' | 'jsonPath' | 'dataType' | 'numericPrecision' | 'numericScale'>, b: typeof a) =>
  a.sourceColumn === b.sourceColumn && a.jsonPath === b.jsonPath && a.dataType === b.dataType && a.numericPrecision === b.numericPrecision && a.numericScale === b.numericScale

export function createMockDatasetsApi(): DatasetsApi {
  const datasets: Dataset[] = structuredClone(seedDatasets)
  const versions: StoredVersion[] = structuredClone(seedVersions)
  const columns: Record<string, DatasetSourceColumn[]> = structuredClone(seedColumns)
  const objects: DatasetSourceObject[] = structuredClone(sourceObjects)

  const columnsOf = (name: string, kind?: DatasetSourceObjectKind) => {
    const obj = objects.find((o) => o.name === name && (!kind || o.kind === kind))
    return obj ? (columns[name] ?? []) : null
  }

  const summary = (v: StoredVersion): DatasetVersionSummary => ({
    id: v.id,
    datasetId: v.datasetId,
    snapshotId: v.snapshotId,
    isLive: v.isLive,
    sourceObject: v.settings.sourceObject,
    createdAt: v.createdAt,
  })

  const liveOf = (datasetId: string) => versions.find((v) => v.datasetId === datasetId && v.isLive)!

  const listItem = (d: Dataset): DatasetListItem => {
    const live = liveOf(d.id)
    const frozen = versions.filter((v) => v.datasetId === d.id && !v.isLive).sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    return {
      ...d,
      versions: [live, ...frozen].map(summary),
      liveVersionId: live.id,
      storage: objects.find((o) => o.name === live.settings.sourceObject)?.stats ?? null,
    }
  }

  const detail = (v: StoredVersion): DatasetVersion => ({
    ...summary(v),
    dataset: { ...datasets.find((d) => d.id === v.datasetId)! },
    settings: v.settings,
    fields: v.fields,
    grains: v.grains,
    bindings: v.bindings,
    measures: v.measures,
    dependents: dependentsOf(v, catalog),
  })

  type Found = { ok: true; v: StoredVersion } | { ok: false; problem: Problem }
  const findVersion = (versionId: string): Found => {
    const v = versions.find((x) => x.id === versionId)
    return v ? { ok: true, v } : problem(404, 'Version not found', {}, { detail: `No version '${versionId}'.` })
  }
  const findLive = (versionId: string): Found => {
    const found = findVersion(versionId)
    if (!found.ok) return found
    if (!found.v.isLive) return problem(409, 'This version is frozen', {}, { detail: 'A frozen snapshot holds it, so it never changes. Edit the live version instead.' })
    return found
  }

  const refuseIfBlocked = (v: StoredVersion, itemId: string, what: string) => {
    const blocking = (dependentsOf(v, catalog)[itemId] ?? []).filter((d) => d.blocksDelete)
    if (!blocking.length) return null
    return problem(409, `This ${what} is in use`, {}, {
      detail: `${blocking.length} ${blocking.length === 1 ? 'item reads' : 'items read'} this ${what}. Remove those references first.`,
      violations: blocking.map((d) => ({
        code: `${what}_in_use`,
        message: `${d.itemKey} reads it as ${d.relation}.`,
        target: { area: d.tab, itemId: d.itemId ?? undefined, itemKey: d.itemKey },
      })),
    })
  }


  const deleteCheck = (d: Dataset): DeleteCheck => {
    const reasons: DeleteBlocker[] = versions
      .filter((v) => v.datasetId === d.id && !v.isLive)
      .map((v) => ({ code: 'referenced_by_snapshot' as const, message: `Snapshot ${seedSnapshots.find((s) => s.id === v.snapshotId)!.key} holds a version of it.` }))
    return { allowed: reasons.length === 0, message: reasons.length ? `Dataset '${d.displayName}' cannot be deleted.` : null, reasons }
  }

  type Materializable = { ok: true; v: StoredVersion; f: DatasetField } | { ok: false; problem: Problem }
  const findMaterializable = (versionId: string, fieldId: string): Materializable => {
    const found = findVersion(versionId)
    if (!found.ok) return found
    if (!found.v.isLive) return problem(409, 'This version is frozen', {}, { detail: 'Only the live version is materialized.' })
    const f = found.v.fields.find((x) => x.id === fieldId)
    if (!f) return problem(404, 'Field not found')
    if (f.fieldSourceKind !== 'json_path') return problem(409, 'Only a JSON path field can be materialized', {}, { detail: 'Column fields already read stored values.' })
    return { ok: true, v: found.v, f }
  }

  /** Simulates the engine's asynchronous build of the materialized view column. */
  const startMaterializing = (v: StoredVersion, f: DatasetField, previousRefresh: string | null = null): DatasetField['materialization'] => {
    const columnName = `${f.sourceColumn}_${(f.jsonPath ?? '').replace(/^\$\./, '').replace(/[^A-Za-z0-9]+/g, '_')}`.toLowerCase()
    const m = { viewName: `mv_${v.settings.sourceObject}`, columnName, state: 'building' as const, requestedAt: new Date().toISOString(), refreshedAt: previousRefresh, error: null }
    finishBuildLater(v.id, f.id, m.requestedAt, MATERIALIZE_MS)
    return m
  }

  const finishBuildLater = (versionId: string, fieldId: string, requestedAt: string, ms: number) =>
    setTimeout(() => {
      const live = versions.find((x) => x.id === versionId)?.fields.find((x) => x.id === fieldId)
      if (!live?.materialization || live.materialization.state !== 'building' || live.materialization.requestedAt !== requestedAt) return
      const unconvertible = live.dataType === 'json'
      live.materialization = unconvertible
        ? { ...live.materialization, state: 'failed', error: 'A json value cannot be stored as a typed view column. Give the field a scalar type.' }
        : { ...live.materialization, state: 'ready', refreshedAt: new Date().toISOString(), error: null }
    }, ms)

  /** An undo/redo can bring back a field whose build timer already fired; give it a fresh one. */
  const resumeBuilds = () => {
    for (const v of versions)
      for (const f of v.fields)
        if (f.materialization?.state === 'building') {
          const left = MATERIALIZE_MS - (Date.now() - Date.parse(f.materialization.requestedAt))
          finishBuildLater(v.id, f.id, f.materialization.requestedAt, Math.max(left, 300))
        }
  }

  const violationsOf = (v: StoredVersion) => commitViolations(v, catalog, (n) => columnsOf(n, v.settings.sourceObjectKind))

  return {
    ...createSnapshotChangeSet(
      () => ({ datasets, versions, columns }),
      (snapshot) => {
        refill(datasets, snapshot.datasets)
        refill(versions, snapshot.versions)
        for (const key of Object.keys(columns)) delete columns[key]
        Object.assign(columns, snapshot.columns)
        resumeBuilds()
      },
      (before) => {
        const saved = new Map(before.versions.map((v) => [v.id, JSON.stringify(v)]))
        return versions.filter((v) => v.isLive && saved.get(v.id) !== JSON.stringify(v)).flatMap(violationsOf)
      },
    ),
    async listSourceObjects() {
      await delay()
      return structuredClone(objects)
    },
    async getSourceColumns(kind, name) {
      await delay()
      const cols = columnsOf(name, kind)
      if (!cols) return problem(404, 'Source object not found', { sourceObject: [`${kind} '${name}' does not exist in the tenant database.`] })
      return ok(cols)
    },

    async listDatasets() {
      await delay()
      return structuredClone(datasets.map(listItem))
    },
    async getDataset(datasetId) {
      await delay()
      const d = datasets.find((x) => x.id === datasetId)
      return d ? ok(listItem(d)) : problem(404, 'Dataset not found')
    },
    async checkDeleteDataset(datasetId) {
      await delay()
      return deleteCheck(datasets.find((x) => x.id === datasetId)!)
    },
    async createDataset(input: DatasetCreateInput) {
      await delay()
      const errors: Errors = {}
      if (!DOTTED_KEY_PATTERN.test(input.key)) err(errors, 'key', 'Start with a letter; use letters, digits, _ and .')
      else if (datasets.some((d) => d.key === input.key)) err(errors, 'key', `'${input.key}' is already a dataset key.`)
      if (!input.displayName.trim()) err(errors, 'displayName', 'Enter a display name.')
      if (input.mode === 'existing') {
        checkVocab(errors, 'sourceObjectKind', 'sourceObjectKind', input.sourceObjectKind)
        if (!input.sourceObject) err(errors, 'sourceObject', 'Choose a table or view.')
        else if (!columnsOf(input.sourceObject, input.sourceObjectKind)) err(errors, 'sourceObject', `${input.sourceObjectKind} '${input.sourceObject}' does not exist in the tenant database.`)
      } else {
        if (!COLUMN_NAME.test(input.tableName)) err(errors, 'tableName', 'Use lower-case letters, digits and _, starting with a letter.')
        else if (objects.some((o) => o.name === input.tableName)) err(errors, 'tableName', `'${input.tableName}' already exists in the tenant database.`)
        const seen = new Set<string>()
        input.columns.forEach((c, i) => {
          if (!COLUMN_NAME.test(c.name)) err(errors, `columns[${i}].name`, 'Lower-case letters, digits and _.')
          else if (seen.has(c.name)) err(errors, `columns[${i}].name`, `'${c.name}' is used twice.`)
          seen.add(c.name)
          checkVocab(errors, `columns[${i}].dataType`, 'dataType', c.dataType)
          if (c.dataType === 'decimal') checkPrecision(errors, `columns[${i}].numeric`, c.numericPrecision, c.numericScale)
        })
      }
      if (Object.keys(errors).length) return problem(400, 'The dataset could not be created', errors)
      const sourceObjectKind: DatasetSourceObjectKind = input.mode === 'existing' ? input.sourceObjectKind : 'table'
      const sourceObject = input.mode === 'existing' ? input.sourceObject : input.tableName
      if (input.mode === 'new') {
        objects.push({ name: input.tableName, kind: 'table', isManaged: true, stats: { rowCount: 0, sizeBytes: 8192, measuredAt: new Date().toISOString() } })
        columns[input.tableName] = input.columns.map((c) => columnFromInput(c))
      }
      const mapped = input.mode === 'new' ? input.columns : (columnsOf(sourceObject, sourceObjectKind) ?? []).flatMap((c) => (c.mappedType ? [{ name: c.name, dataType: c.mappedType, numericPrecision: c.numericPrecision, numericScale: c.numericScale, isNullable: c.isNullable }] : []))
      const fields: DatasetField[] = mapped.map((c) => ({
          id: newId('f'),
          key: camel(c.name),
          displayName: title(c.name),
          fieldSourceKind: 'column' as const,
          sourceColumn: c.name,
          jsonPath: null,
          expressionText: null,
          materialization: null,
          dataType: c.dataType,
          numericPrecision: c.dataType === 'decimal' ? c.numericPrecision : null,
          numericScale: c.dataType === 'decimal' ? c.numericScale : null,
          isNullable: c.isNullable,
          unitKey: null,
          isSelectable: true,
          isFilterable: true,
          isSortable: c.dataType !== 'json',
          isGroupable: c.dataType === 'string' || TIME_TYPES.has(c.dataType) || c.dataType === 'boolean',
          isEnabled: true,
        }))
      const d: Dataset = { id: newId('ds'), key: input.key, displayName: input.displayName.trim(), isEnabled: false }
      datasets.push(d)
      versions.push({
        id: newId('v'),
        datasetId: d.id,
        snapshotId: LIVE_SNAPSHOT_ID,
        isLive: true,
        createdAt: new Date().toISOString(),
        settings: { sourceObjectKind, sourceObject, supportsAggregate: true, supportsDetail: true, defaultTimeBindingId: null, expressionLanguageVersion: '1.0', securityPolicyKey: null, properties: '{}' },
        fields,
        grains: [],
        bindings: [],
        measures: [],
      })
      return ok(listItem(d))
    },
    async renameSourceTable(versionId, tableName) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const { v } = found
      const from = v.settings.sourceObject
      if (tableName === from) return ok(v.settings)
      const obj = objects.find((o) => o.name === from && o.kind === 'table')
      if (!obj?.isManaged || v.settings.sourceObjectKind !== 'table') return problem(409, 'This table cannot be renamed', {}, { detail: 'Only tables Meridian created can be renamed.' })
      if (versions.some((x) => x.id !== v.id && x.settings.sourceObject === from))
        return problem(409, 'This table cannot be renamed', {}, { detail: `Another version reads '${from}'.` })
      if (!COLUMN_NAME.test(tableName)) return problem(400, 'The table could not be renamed', { tableName: ['Use lower-case letters, digits and _, starting with a letter.'] })
      if (objects.some((o) => o.name === tableName)) return problem(400, 'The table could not be renamed', { tableName: [`'${tableName}' already exists in the tenant database.`] })
      obj.name = tableName
      columns[tableName] = columns[from] ?? []
      delete columns[from]
      v.settings = { ...v.settings, sourceObject: tableName }
      return ok(v.settings)
    },
    async updateDataset(datasetId, patch) {
      await delay()
      const d = datasets.find((x) => x.id === datasetId)
      if (!d) return problem(404, 'Dataset not found')
      const errors: Errors = {}
      if (patch.displayName !== undefined && !patch.displayName.trim()) err(errors, 'displayName', 'Enter a display name.')
      if (Object.keys(errors).length) return problem(400, 'The dataset could not be saved', errors)
      Object.assign(d, patch)
      return ok(d)
    },
    async deleteDataset(datasetId) {
      await delay()
      const d = datasets.find((x) => x.id === datasetId)
      if (!d) return problem(404, 'Dataset not found')
      const check = deleteCheck(d)
      if (!check.allowed) return problem(409, 'This dataset cannot be deleted', {}, { detail: check.message!, violations: check.reasons })
      for (const table of managedTablesDroppedWith(d.id, versions.map((v) => ({ datasetId: v.datasetId, sourceObject: v.settings.sourceObject })), objects)) {
        objects.splice(objects.findIndex((o) => o.name === table), 1)
        delete columns[table]
      }
      datasets.splice(datasets.indexOf(d), 1)
      versions.filter((v) => v.datasetId === d.id).forEach((v) => versions.splice(versions.indexOf(v), 1))
      return ok(null)
    },

    async getVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      return found.ok ? ok(detail(found.v)) : found
    },
    async saveField(versionId, input: DatasetFieldInput) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.fields.find((f) => f.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Field not found')
      const errors: Errors = {}
      if (existing && existing.key !== input.key) err(errors, 'key', 'The field key is fixed once created.')
      else if (!KEY_PATTERN.test(input.key)) err(errors, 'key', 'Start with a letter; use letters, digits and _.')
      else if (!existing && v.fields.some((f) => f.key === input.key)) err(errors, 'key', `Another field of this version uses '${input.key}'.`)
      if (!input.displayName.trim()) err(errors, 'displayName', 'Enter a display name.')
      checkVocab(errors, 'fieldSourceKind', 'fieldSourceKind', input.fieldSourceKind)
      checkVocab(errors, 'dataType', 'dataType', input.dataType)
      const cols = columnsOf(v.settings.sourceObject, v.settings.sourceObjectKind) ?? []
      const col = (name: string | null) => cols.find((x) => x.name === name)
      const { createColumn, materialize, ...rest } = input
      const next: DatasetField = { ...rest, id: existing?.id ?? newId('f'), displayName: input.displayName.trim(), materialization: existing?.materialization ?? null }
      const creating = !!createColumn && input.fieldSourceKind === 'column'

      if (creating) {
        if (v.settings.sourceObjectKind !== 'table') err(errors, 'sourceColumn', `${v.settings.sourceObject} is a view; only a table can take new columns.`)
        else if (!input.sourceColumn || !COLUMN_NAME.test(input.sourceColumn)) err(errors, 'sourceColumn', 'Use lower-case letters, digits and _, starting with a letter.')
        else if (col(input.sourceColumn)) err(errors, 'sourceColumn', `'${input.sourceColumn}' already exists; choose it from the list instead.`)
        next.jsonPath = null
        next.expressionText = null
      } else if (input.fieldSourceKind === 'column' || input.fieldSourceKind === 'json_path') {
        if (!input.sourceColumn) err(errors, 'sourceColumn', 'Choose a column.')
        else if (!col(input.sourceColumn)) err(errors, 'sourceColumn', `'${input.sourceColumn}' is not a column of ${v.settings.sourceObject}.`)
        else if (!col(input.sourceColumn)!.mappedType) err(errors, 'sourceColumn', `Fields cannot read ${col(input.sourceColumn)!.nativeType} columns.`)
        else if (input.fieldSourceKind === 'json_path' && col(input.sourceColumn)!.mappedType !== 'json') err(errors, 'sourceColumn', 'A JSON path reads a json column.')
        next.expressionText = null
      }
      if (input.fieldSourceKind === 'column') next.jsonPath = null
      if (input.fieldSourceKind === 'json_path') {
        if (!input.jsonPath || !JSON_PATH.test(input.jsonPath)) err(errors, 'jsonPath', 'Write the path as $.a.b or $.a[0].')
      }
      if (input.fieldSourceKind === 'expression') {
        next.sourceColumn = null
        next.jsonPath = null
        if (!input.expressionText?.trim()) err(errors, 'expressionText', 'Enter an expression.')
        else checkBrackets(errors, 'expressionText', input.expressionText)
      }
      if (input.dataType === 'decimal') checkPrecision(errors, 'numeric', input.numericPrecision, input.numericScale)
      else {
        next.numericPrecision = null
        next.numericScale = null
      }
      next.unitKey = input.unitKey?.trim() || null
      if (Object.keys(errors).length) return problem(400, 'The field could not be saved', errors)
      if (input.fieldSourceKind !== 'json_path' || (existing && !sameSource(existing, next))) next.materialization = null
      if (creating) (columns[v.settings.sourceObject] ??= []).push(columnFromInput({ name: input.sourceColumn!, dataType: input.dataType, numericPrecision: next.numericPrecision, numericScale: next.numericScale, isNullable: input.isNullable }, true))
      if (materialize && input.fieldSourceKind === 'json_path' && !next.materialization) next.materialization = startMaterializing(v, next)
      if (existing) Object.assign(existing, next)
      else v.fields.push(next)
      return ok(next)
    },
    async deleteField(versionId, fieldId) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const blocked = refuseIfBlocked(found.v, fieldId, 'field')
      if (blocked) return blocked
      found.v.fields = found.v.fields.filter((f) => f.id !== fieldId)
      return ok(null)
    },
    async addFieldsFromColumns(versionId, names) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const cols = columnsOf(v.settings.sourceObject, v.settings.sourceObjectKind) ?? []
      const backed = new Set(v.fields.filter((f) => f.fieldSourceKind === 'column').map((f) => f.sourceColumn))
      const errors: Errors = {}
      names.forEach((n, i) => {
        const c = cols.find((x) => x.name === n)
        if (!c) err(errors, `columns[${i}]`, `'${n}' is not a column of ${v.settings.sourceObject}.`)
        else if (!c.mappedType) err(errors, `columns[${i}]`, `Fields cannot read ${c.nativeType} columns.`)
        else if (backed.has(n)) err(errors, `columns[${i}]`, `'${n}' already backs a field.`)
      })
      if (!names.length) err(errors, 'columns', 'Choose at least one column.')
      if (Object.keys(errors).length) return problem(400, 'The fields could not be created', errors)
      const keys = new Set(v.fields.map((f) => f.key))
      const created = names.map((n) => {
        const c = cols.find((x) => x.name === n)!
        let key = camel(n)
        while (keys.has(key)) key = `${key}_`
        keys.add(key)
        const f: DatasetField = {
          id: newId('f'),
          key,
          displayName: title(n),
          fieldSourceKind: 'column',
          sourceColumn: n,
          jsonPath: null,
          expressionText: null,
          materialization: null,
          dataType: c.mappedType!,
          numericPrecision: c.mappedType === 'decimal' ? c.numericPrecision : null,
          numericScale: c.mappedType === 'decimal' ? c.numericScale : null,
          isNullable: c.isNullable,
          unitKey: null,
          isSelectable: true,
          isFilterable: true,
          isSortable: c.mappedType !== 'json',
          isGroupable: c.mappedType === 'string' || TIME_TYPES.has(c.mappedType!) || c.mappedType === 'boolean',
          isEnabled: true,
        }
        v.fields.push(f)
        return f
      })
      return ok(created)
    },
    async materializeField(versionId, fieldId) {
      await delay()
      const found = findMaterializable(versionId, fieldId)
      if (!found.ok) return found
      if (found.f.materialization && found.f.materialization.state !== 'failed') return ok(found.f)
      found.f.materialization = startMaterializing(found.v, found.f)
      return ok(found.f)
    },
    async refreshMaterialization(versionId, fieldId) {
      await delay()
      const found = findMaterializable(versionId, fieldId)
      if (!found.ok) return found
      if (!found.f.materialization) return problem(409, 'This field is not materialized', {}, { detail: 'Materialize it first.' })
      found.f.materialization = startMaterializing(found.v, found.f, found.f.materialization.refreshedAt)
      return ok(found.f)
    },
    async dematerializeField(versionId, fieldId) {
      await delay()
      const found = findMaterializable(versionId, fieldId)
      if (!found.ok) return found
      found.f.materialization = null
      return ok(found.f)
    },

    async saveGrain(versionId, input: DatasetGrainInput) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.grains.find((g) => g.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Grain not found')
      const errors: Errors = {}
      if (existing && existing.key !== input.key) err(errors, 'key', 'The grain key is fixed once created.')
      else if (!KEY_PATTERN.test(input.key)) err(errors, 'key', 'Start with a letter; use letters, digits and _.')
      checkVocab(errors, 'grainKind', 'grainKind', input.grainKind)
      if (!input.fieldIds.length) err(errors, 'fieldIds', 'Add at least one component field.')
      if (new Set(input.fieldIds).size !== input.fieldIds.length) err(errors, 'fieldIds', 'A field can appear only once.')
      if (input.fieldIds.some((id) => !v.fields.some((f) => f.id === id))) err(errors, 'fieldIds', 'Choose fields of this version.')
      if (Object.keys(errors).length) return problem(400, 'The grain could not be saved', errors)
      const next: DatasetGrain = { ...input, id: existing?.id ?? newId('g') }
      if (existing) Object.assign(existing, next)
      else v.grains.push(next)
      return ok(next)
    },
    async deleteGrain(versionId, grainId) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const blocked = refuseIfBlocked(found.v, grainId, 'grain')
      if (blocked) return blocked
      found.v.grains = found.v.grains.filter((g) => g.id !== grainId)
      return ok(null)
    },

    async saveBinding(versionId, input: DatasetBindingInput) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.bindings.find((b) => b.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Binding not found')
      const errors: Errors = {}
      const next: DatasetBinding = structuredClone({ ...input, id: existing?.id ?? newId('b') })
      checkVocab(errors, 'bindingKind', 'bindingKind', input.bindingKind)
      checkVocab(errors, 'raggedBehavior', 'raggedBehavior', input.raggedBehavior)
      checkVocab(errors, 'defaultPeriodAlignment', 'periodAlignment', input.defaultPeriodAlignment)
      checkVocab(errors, 'unmappedBehavior', 'unmappedBehavior', input.unmappedBehavior)
      const field = (id: string | null) => v.fields.find((f) => f.id === id)
      const kind = input.bindingKind
      const needsField = kind !== 'bridge'

      if (kind === 'time') {
        next.dimensionRoleId = null
        next.mappingSetId = null
        next.bridgeId = null
        next.sourceDimensionBindingId = null
        if (!input.dimensionId) err(errors, 'dimensionId', 'Choose a time dimension.')
        else if (!catalog.dimensions.some((d) => d.id === input.dimensionId)) err(errors, 'dimensionId', 'Choose a dimension of this tenant.')
      } else {
        next.dimensionId = null
        next.time = null
        if (!input.dimensionRoleId) err(errors, 'dimensionRoleId', 'Choose a role.')
        else if (!catalog.roles.some((r) => r.id === input.dimensionRoleId)) err(errors, 'dimensionRoleId', 'Choose a role of this tenant.')
        else if (v.bindings.some((b) => b.id !== next.id && b.dimensionRoleId === input.dimensionRoleId)) err(errors, 'dimensionRoleId', 'This role is already bound in this version.')
      }
      if (needsField && !input.sourceFieldId) err(errors, 'sourceFieldId', 'Choose a source field.')
      if (input.sourceFieldId && !field(input.sourceFieldId)) err(errors, 'sourceFieldId', 'Choose a field of this version.')
      if (kind === 'mapped') {
        if (!input.mappingSetId) err(errors, 'mappingSetId', 'Choose a mapping set.')
      } else next.mappingSetId = null
      if (kind === 'bridge') {
        if (!input.sourceDimensionBindingId) err(errors, 'sourceDimensionBindingId', 'Choose the binding to remap.')
        else if (input.sourceDimensionBindingId === next.id) err(errors, 'sourceDimensionBindingId', 'A binding cannot bridge from itself.')
        if (!input.bridgeId) err(errors, 'bridgeId', 'Choose a bridge.')
      } else {
        next.bridgeId = null
        next.sourceDimensionBindingId = null
      }
      if (input.unmappedBehavior === 'unknown') {
        if (!input.unknownMemberKey) err(errors, 'unknownMemberKey', 'Choose the member unmapped values group under.')
      } else next.unknownMemberKey = null
      if (existing?.isRequired && !input.isEnabled) err(errors, 'isEnabled', 'A required binding cannot be disabled.')
      next.isRequired = existing?.isRequired ?? false

      if (kind === 'time') {
        const t = input.time
        if (!t) err(errors, 'time', 'A time binding needs its time axis settings.')
        else {
          const src = field(input.sourceFieldId)
          if (src && !TIME_TYPES.has(src.dataType)) err(errors, 'sourceFieldId', `A time axis reads a date or timestamp field; '${src.key}' is ${src.dataType}.`)
          checkVocab(errors, 'time.timeValueType', 'timeValueType', t.timeValueType)
          checkVocab(errors, 'time.observationKind', 'observationKind', t.observationKind)
          checkVocab(errors, 'time.nativePeriodicity', 'periodicity', t.nativePeriodicity, true)
          checkVocab(errors, 'time.cumulativeOrigin', 'cumulativeOrigin', t.cumulativeOrigin)
          checkVocab(errors, 'time.defaultQueryTimeZone', 'timeZone', t.defaultQueryTimeZone)
          if (t.timeValueType === 'timestamp') checkVocab(errors, 'time.sourceTimeZone', 'timeZone', t.sourceTimeZone)
          else next.time!.sourceTimeZone = null
          if (!t.allowedPeriodicities.length) err(errors, 'time.allowedPeriodicities', 'Allow at least one periodicity.')
          t.allowedPeriodicities.forEach((p) => checkVocab(errors, 'time.allowedPeriodicities', 'periodicity', p))
          if (t.intervalEndFieldId && !field(t.intervalEndFieldId)) err(errors, 'time.intervalEndFieldId', 'Choose a field of this version.')
          if (t.cumulativeOrigin === 'fixed_start') {
            if (!t.cumulativeFixedStart || !ISO_DATE.test(t.cumulativeFixedStart)) err(errors, 'time.cumulativeFixedStart', 'Enter the start as YYYY-MM-DD.')
          } else next.time!.cumulativeFixedStart = null
        }
      }
      if (Object.keys(errors).length) return problem(400, 'The binding could not be saved', errors)
      if (existing) Object.assign(existing, next)
      else v.bindings.push(next)
      return ok(next)
    },
    async deleteBinding(versionId, bindingId) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const b = found.v.bindings.find((x) => x.id === bindingId)
      if (!b) return problem(404, 'Binding not found')
      if (b.isRequired) return problem(409, 'This binding is required', {}, { detail: 'Required bindings are fixtures of the dataset and cannot be deleted.' })
      const blocked = refuseIfBlocked(found.v, bindingId, 'binding')
      if (blocked) return blocked
      found.v.bindings = found.v.bindings.filter((x) => x.id !== bindingId)
      return ok(null)
    },

    async saveMeasure(versionId, input: DatasetMeasureInput) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.measures.find((m) => m.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Measure not found')
      const errors: Errors = {}
      const next: DatasetMeasure = structuredClone({ ...input, id: existing?.id ?? newId('m'), displayName: input.displayName.trim() })
      if (existing && existing.key !== input.key) err(errors, 'key', 'The measure key is fixed once created.')
      else if (!KEY_PATTERN.test(input.key)) err(errors, 'key', 'Start with a letter; use letters, digits and _.')
      if (!next.displayName) err(errors, 'displayName', 'Enter a display name.')
      checkVocab(errors, 'measureKind', 'measureKind', input.measureKind)
      const kind = input.measureKind
      const roles = vocabularies.componentRolesByKind[kind] ?? []

      if (kind === 'aggregate') {
        if (!input.aggregationFunction) err(errors, 'aggregationFunction', 'Choose how the value aggregates.')
        else checkVocab(errors, 'aggregationFunction', 'aggregationFunction', input.aggregationFunction)
      } else if (kind === 'specialized') checkVocab(errors, 'aggregationFunction', 'aggregationFunction', input.aggregationFunction, true)
      else next.aggregationFunction = null

      if (composesMeasures(kind)) {
        next.components = []
        if (!input.expressionText?.trim()) err(errors, 'expressionText', 'Enter an expression over other measures, e.g. [m.a] / [m.b].')
        else {
          checkBrackets(errors, 'expressionText', input.expressionText)
          if (refsOf(input.expressionText, MEASURE_REF).includes(input.key)) err(errors, 'expressionText', 'An expression cannot reference its own measure.')
          if (refsOf(input.expressionText, FIELD_REF).length) err(errors, 'expressionText', 'Expressions compose measures ([m.key]); fields belong in components.')
        }
      } else next.expressionText = null

      if (kind === 'compound') {
        if (!input.compoundingPeriodicity) err(errors, 'compoundingPeriodicity', 'Choose the step the rate compounds over.')
        else checkVocab(errors, 'compoundingPeriodicity', 'periodicity', input.compoundingPeriodicity)
        if (!input.compoundingForm) err(errors, 'compoundingForm', 'Choose how the steps link.')
        else checkVocab(errors, 'compoundingForm', 'compoundingForm', input.compoundingForm)
      } else {
        next.compoundingPeriodicity = null
        next.compoundingForm = null
      }

      if (kind === 'specialized') {
        if (!input.implementationKey?.trim()) err(errors, 'implementationKey', 'Enter the implementation key.')
        if (!input.implementationVersion?.trim()) err(errors, 'implementationVersion', 'Enter the implementation version.')
        if (!parsesAsJson(input.configurationJson)) err(errors, 'configurationJson', 'The configuration is not valid JSON.')
      } else {
        next.implementationKey = null
        next.implementationVersion = null
        next.configurationJson = null
      }

      const allowed = new Set(roles.map((r) => r.role))
      input.components.forEach((cp) => {
        if (!allowed.has(cp.role)) err(errors, `components.${cp.role}`, `A ${kind} measure does not read a ${cp.role} component.`)
        if (!v.fields.some((f) => f.id === cp.fieldId)) err(errors, `components.${cp.role}`, 'Choose a field of this version.')
      })
      roles.filter((r) => r.required && !input.components.some((cp) => cp.role === r.role)).forEach((r) => err(errors, `components.${r.role}`, `A ${kind} measure needs a ${r.role} field.`))
      if (new Set(input.components.map((cp) => cp.role)).size !== input.components.length) err(errors, 'components', 'Each role takes one field.')

      if (input.rowFilterExpression?.trim()) checkBrackets(errors, 'rowFilterExpression', input.rowFilterExpression)
      else next.rowFilterExpression = null

      checkVocab(errors, 'valueNullBehavior', 'valueNullBehavior', input.valueNullBehavior)
      checkVocab(errors, 'negativeWeightBehavior', 'negativeWeightBehavior', input.negativeWeightBehavior)
      checkVocab(errors, 'emptySetBehavior', 'emptySetBehavior', input.emptySetBehavior)
      checkVocab(errors, 'bridgeFactorBehavior', 'bridgeFactorBehavior', input.bridgeFactorBehavior)
      checkVocab(errors, 'bridgeAllocationBehavior', 'bridgeAllocationBehavior', input.bridgeAllocationBehavior)
      checkVocab(errors, 'totalBehavior', 'totalBehavior', input.totalBehavior)
      checkVocab(errors, 'outputDataType', 'measureOutputDataType', input.outputDataType)
      checkVocab(errors, 'roundingMode', 'roundingMode', input.roundingMode, true)
      if (input.outputDataType === 'decimal') checkPrecision(errors, 'output', input.outputPrecision, input.outputScale)
      else {
        next.outputPrecision = null
        next.outputScale = null
      }
      checkPrecision(errors, 'accumulator', input.accumulatorPrecision, input.accumulatorScale)
      next.unitKey = input.unitKey?.trim() || null

      if (kind === 'period_average' && !input.timePolicy) err(errors, 'timePolicy', 'A period average always has a time policy.')
      const tp = input.timePolicy
      if (tp) {
        checkVocab(errors, 'timePolicy.observationSelection', 'observationSelection', tp.observationSelection)
        checkVocab(errors, 'timePolicy.valueTimeRole', 'valueTimeRole', tp.valueTimeRole)
        checkVocab(errors, 'timePolicy.boundaryMatch', 'boundaryMatch', tp.boundaryMatch)
        checkVocab(errors, 'timePolicy.durationUnit', 'durationUnit', tp.durationUnit)
        checkVocab(errors, 'timePolicy.partialIntervalBehavior', 'partialIntervalBehavior', tp.partialIntervalBehavior)
        checkVocab(errors, 'timePolicy.missingPeriodBehavior', 'missingPeriodBehavior', tp.missingPeriodBehavior)
        if (tp.requiredTimeBindingId && !v.bindings.some((b) => b.id === tp.requiredTimeBindingId)) err(errors, 'timePolicy.requiredTimeBindingId', 'Choose a time binding of this version.')
        if (tp.selectionDatasetGrainId && !v.grains.some((g) => g.id === tp.selectionDatasetGrainId)) err(errors, 'timePolicy.selectionDatasetGrainId', 'Choose a grain of this version.')
        if (tp.observationSelection !== 'all' && !tp.selectionDatasetGrainId) err(errors, 'timePolicy.selectionDatasetGrainId', `'${tp.observationSelection}' needs the series grain it selects by.`)
        if (tp.boundaryMatch === 'exact') next.timePolicy!.maxLookbackSeconds = null
        else if (tp.maxLookbackSeconds === null || !Number.isFinite(tp.maxLookbackSeconds) || tp.maxLookbackSeconds <= 0)
          err(errors, 'timePolicy.maxLookbackSeconds', 'A look-back match needs a finite look-back greater than 0.')
      }
      if (Object.keys(errors).length) return problem(400, 'The measure could not be saved', errors)
      if (existing) Object.assign(existing, next)
      else v.measures.push(next)
      return ok(next)
    },
    async deleteMeasure(versionId, measureId) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      found.v.measures = found.v.measures.filter((m) => m.id !== measureId)
      return ok(null)
    },

    async saveSettings(versionId, s: DatasetVersionSettings) {
      await delay()
      const found = findLive(versionId)
      if (!found.ok) return found
      const v = found.v
      const errors: Errors = {}
      checkVocab(errors, 'sourceObjectKind', 'sourceObjectKind', s.sourceObjectKind)
      if (!columnsOf(s.sourceObject, s.sourceObjectKind)) err(errors, 'sourceObject', `${s.sourceObjectKind} '${s.sourceObject}' does not exist in the tenant database.`)
      checkVocab(errors, 'expressionLanguageVersion', 'expressionLanguageVersion', s.expressionLanguageVersion)
      if (s.defaultTimeBindingId && v.bindings.find((b) => b.id === s.defaultTimeBindingId)?.bindingKind !== 'time') err(errors, 'defaultTimeBindingId', 'Choose a time binding of this version.')
      if (!parsesAsJson(s.properties)) err(errors, 'properties', 'Properties must be valid JSON.')
      if (Object.keys(errors).length) return problem(400, 'The settings could not be saved', errors)
      v.settings = { ...s, securityPolicyKey: s.securityPolicyKey?.trim() || null, properties: s.properties.trim() || '{}' }
      const warnings = violationsOf(v).filter((x) => x.code === 'field_columns_exist')
      return ok({ settings: v.settings, warnings })
    },
  }
}
