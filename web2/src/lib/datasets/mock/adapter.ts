import type {
  ApiResult,
  BindingInput,
  CreateDatasetInput,
  CreateDraftInput,
  Dataset,
  DatasetBinding,
  DatasetField,
  DatasetGrain,
  DatasetListItem,
  DatasetMeasure,
  DatasetsApi,
  DatasetVersion,
  DatasetVersionSettings,
  DatasetVersionSummary,
  Dependent,
  FieldInput,
  GrainInput,
  MeasureInput,
  NewColumnInput,
  SourceObject,
  SourceColumn,
  SourceObjectKind,
  ValidationProblemDetails,
  ValidationReport,
  Vocabularies,
} from '../contract'
import { createSnapshotChangeSet, refill } from '@/lib/api/changes'
import { catalog as seedCatalog, reId, seedDatasets, seedReleasePins, seedVersions, sourceColumns as seedColumns, sourceObjects, vocabularies, type StoredVersion } from './data'
import { dependentsOf, FIELD_REF, MEASURE_REF, publicationViolations, refsOf } from './rules'

const DATASET_KEY = /^[A-Za-z0-9][A-Za-z0-9_.-]*$/
const ITEM_KEY = /^[A-Za-z][A-Za-z0-9_]*$/
const JSON_PATH = /^\$(\.[A-Za-z_][A-Za-z0-9_]*|\[\d+\])+$/
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/
const TIME_TYPES = new Set(['date', 'datetime', 'timestamp'])

type Errors = Record<string, string[]>

function problem(status: ValidationProblemDetails['status'], title: string, errors: Errors = {}, extra: Partial<ValidationProblemDetails> = {}): { ok: false; problem: ValidationProblemDetails } {
  return { ok: false, problem: { type: `https://errors.meridian.example/${status}`, title, status, errors, ...extra } }
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

function nativeTypeOf(c: Pick<NewColumnInput, 'dataType' | 'numericPrecision' | 'numericScale'>) {
  const map: Record<string, string> = { boolean: 'boolean', int32: 'integer', int64: 'bigint', string: 'text', date: 'date', datetime: 'timestamp', timestamp: 'timestamptz', json: 'jsonb' }
  return c.dataType === 'decimal' ? `numeric(${c.numericPrecision ?? 18},${c.numericScale ?? 2})` : (map[c.dataType] ?? 'text')
}

function columnFromInput(c: NewColumnInput, generated = false): SourceColumn {
  return {
    name: c.name,
    nativeType: nativeTypeOf(c),
    mappedType: c.dataType,
    numericPrecision: c.dataType === 'decimal' ? c.numericPrecision : null,
    numericScale: c.dataType === 'decimal' ? c.numericScale : null,
    isNullable: c.isNullable,
    isGenerated: generated || undefined,
  }
}

const sameSource = (a: Pick<DatasetField, 'sourceColumn' | 'jsonPath' | 'dataType' | 'numericPrecision' | 'numericScale'>, b: typeof a) =>
  a.sourceColumn === b.sourceColumn && a.jsonPath === b.jsonPath && a.dataType === b.dataType && a.numericPrecision === b.numericPrecision && a.numericScale === b.numericScale

export function createMockDatasetsApi(): DatasetsApi {
  const datasets: Dataset[] = structuredClone(seedDatasets)
  const versions: StoredVersion[] = structuredClone(seedVersions)
  const columns: Record<string, SourceColumn[]> = structuredClone(seedColumns)
  const objects: SourceObject[] = structuredClone(sourceObjects)
  const catalog = structuredClone(seedCatalog)
  catalog.releases.forEach((r) => (r.pins = structuredClone(seedReleasePins[r.id] ?? [])))

  const columnsOf = (name: string, kind?: SourceObjectKind) => {
    const obj = objects.find((o) => o.name === name && (!kind || o.kind === kind))
    return obj ? (columns[name] ?? []) : null
  }

  const summary = (v: StoredVersion): DatasetVersionSummary => ({
    id: v.id,
    datasetId: v.datasetId,
    versionNumber: v.versionNumber,
    status: v.status,
    sourceObject: v.settings.sourceObject,
    createdAt: v.createdAt,
    publishedAt: v.publishedAt,
    clonedFromVersionId: v.clonedFromVersionId,
    releases: catalog.releases
      .filter((r) => r.pins.some((p) => p.versionId === v.id))
      .map((r) => ({ releaseId: r.id, releaseKey: r.releaseKey, isActive: r.isActive })),
  })

  const listItem = (d: Dataset): DatasetListItem => {
    const active = catalog.releases.find((r) => r.isActive)?.pins.find((p) => p.datasetId === d.id)
    const own = versions.filter((v) => v.datasetId === d.id).sort((a, b) => b.versionNumber - a.versionNumber)
    const source = own[0] && objects.find((o) => o.name === own[0].settings.sourceObject)
    return {
      ...d,
      versions: own.map(summary),
      activeVersionId: active?.versionId ?? null,
      storage: source?.stats ?? null,
    }
  }

  const detail = (v: StoredVersion): DatasetVersion => ({
    ...summary(v),
    settings: v.settings,
    fields: v.fields,
    grains: v.grains,
    bindings: v.bindings,
    measures: v.measures,
    dependents: dependentsOf(v, catalog),
  })

  type Found = { ok: true; v: StoredVersion } | { ok: false; problem: ValidationProblemDetails }
  const findVersion = (versionId: string): Found => {
    const v = versions.find((x) => x.id === versionId)
    return v ? { ok: true, v } : problem(404, 'Version not found', {}, { detail: `No version '${versionId}'.` })
  }
  const findDraft = (versionId: string): Found => {
    const found = findVersion(versionId)
    if (!found.ok) return found
    if (found.v.status !== 'draft')
      return problem(409, 'This version is read-only', {}, { detail: `Version ${found.v.versionNumber} is ${found.v.status}. Only a draft can be edited; clone it to make changes.` })
    return found
  }

  const refuseIfBlocked = (v: StoredVersion, itemId: string, what: string) => {
    const blocking = (dependentsOf(v, catalog)[itemId] ?? []).filter((d) => d.blocksDelete)
    if (!blocking.length) return null
    return problem(409, `This ${what} is in use`, {}, {
      detail: `${blocking.length} ${blocking.length === 1 ? 'item reads' : 'items read'} this ${what}. Remove those references first.`,
      dependents: blocking,
    })
  }

  type Materializable = { ok: true; v: StoredVersion; f: DatasetField } | { ok: false; problem: ValidationProblemDetails }
  const findMaterializable = (versionId: string, fieldId: string): Materializable => {
    const found = findVersion(versionId)
    if (!found.ok) return found
    if (found.v.status === 'retired') return problem(409, 'This version is retired', {}, { detail: 'Retired versions are not read by the engine, so there is nothing to speed up.' })
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

  const report = (v: StoredVersion): ValidationReport => ({
    versionId: v.id,
    status: v.status,
    violations: publicationViolations(v, catalog, (n) => columnsOf(n, v.settings.sourceObjectKind)),
    checkedAt: new Date().toISOString(),
  })

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
    ),
    async getVocabularies() {
      await delay()
      return structuredClone(vocabularies)
    },
    async getTenantCatalog() {
      await delay()
      return structuredClone(catalog)
    },
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
    async createDataset(input: CreateDatasetInput) {
      await delay()
      const errors: Errors = {}
      if (!DATASET_KEY.test(input.datasetKey)) err(errors, 'datasetKey', 'Use letters, digits, _ . or -, starting with a letter or digit.')
      else if (datasets.some((d) => d.datasetKey === input.datasetKey)) err(errors, 'datasetKey', `'${input.datasetKey}' is already a dataset key.`)
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
      const sourceObjectKind: SourceObjectKind = input.mode === 'existing' ? input.sourceObjectKind : 'table'
      const sourceObject = input.mode === 'existing' ? input.sourceObject : input.tableName
      if (input.mode === 'new') {
        objects.push({ name: input.tableName, kind: 'table', isManaged: true, stats: { rowCount: 0, sizeBytes: 8192, measuredAt: new Date().toISOString() } })
        columns[input.tableName] = input.columns.map((c) => columnFromInput(c))
      }
      const mapped = input.mode === 'new' ? input.columns : (columnsOf(sourceObject, sourceObjectKind) ?? []).flatMap((c) => (c.mappedType ? [{ name: c.name, dataType: c.mappedType, numericPrecision: c.numericPrecision, numericScale: c.numericScale, isNullable: c.isNullable }] : []))
      const fields: DatasetField[] = mapped.map((c) => ({
          id: newId('f'),
          fieldKey: camel(c.name),
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
      const d: Dataset = { id: newId('ds'), datasetKey: input.datasetKey, displayName: input.displayName.trim(), isEnabled: false }
      datasets.push(d)
      versions.push({
        id: newId('v'),
        datasetId: d.id,
        versionNumber: 1,
        status: 'draft',
        createdAt: new Date().toISOString(),
        publishedAt: null,
        clonedFromVersionId: null,
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
      const found = findDraft(versionId)
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
      if (patch.datasetKey !== undefined) {
        if (!DATASET_KEY.test(patch.datasetKey)) err(errors, 'datasetKey', 'Use letters, digits, _ . or -, starting with a letter or digit.')
        else if (datasets.some((x) => x.id !== d.id && x.datasetKey === patch.datasetKey)) err(errors, 'datasetKey', `'${patch.datasetKey}' is already a dataset key.`)
      }
      if (patch.displayName !== undefined && !patch.displayName.trim()) err(errors, 'displayName', 'Enter a display name.')
      if (Object.keys(errors).length) return problem(400, 'The dataset could not be saved', errors)
      Object.assign(d, patch)
      return ok(d)
    },
    async deleteDataset(datasetId) {
      await delay()
      const d = datasets.find((x) => x.id === datasetId)
      if (!d) return problem(404, 'Dataset not found')
      const own = versions.filter((v) => v.datasetId === d.id)
      const nonDraft = own.filter((v) => v.status !== 'draft')
      const pinnedBy = catalog.releases.filter((r) => r.pins.some((p) => p.datasetId === d.id))
      if (nonDraft.length || pinnedBy.length)
        return problem(409, 'This dataset cannot be deleted', {}, {
          detail: [
            nonDraft.length ? `It has ${nonDraft.length} non-draft ${nonDraft.length === 1 ? 'version' : 'versions'} (${nonDraft.map((v) => `v${v.versionNumber} ${v.status}`).join(', ')}).` : '',
            pinnedBy.length ? `It is listed in ${pinnedBy.map((r) => r.releaseKey).join(', ')}.` : '',
          ]
            .filter(Boolean)
            .join(' '),
        })
      datasets.splice(datasets.indexOf(d), 1)
      own.forEach((v) => versions.splice(versions.indexOf(v), 1))
      return ok(null)
    },

    async getVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      return found.ok ? ok(detail(found.v)) : found
    },
    async createDraft(datasetId, input: CreateDraftInput) {
      await delay()
      if (!datasets.some((d) => d.id === datasetId)) return problem(404, 'Dataset not found')
      const own = versions.filter((v) => v.datasetId === datasetId)
      const versionNumber = Math.max(0, ...own.map((v) => v.versionNumber)) + 1
      const base = { id: newId('v'), datasetId, versionNumber, status: 'draft' as const, createdAt: new Date().toISOString(), publishedAt: null }
      if (input.mode === 'empty') {
        const errors: Errors = {}
        checkVocab(errors, 'sourceObjectKind', 'sourceObjectKind', input.sourceObjectKind)
        if (!columnsOf(input.sourceObject, input.sourceObjectKind)) err(errors, 'sourceObject', `${input.sourceObjectKind} '${input.sourceObject}' does not exist in the tenant database.`)
        if (Object.keys(errors).length) return problem(400, 'The draft could not be created', errors)
        const v: StoredVersion = {
          ...base,
          clonedFromVersionId: null,
          settings: { sourceObjectKind: input.sourceObjectKind, sourceObject: input.sourceObject, supportsAggregate: true, supportsDetail: true, defaultTimeBindingId: null, expressionLanguageVersion: '1.0', securityPolicyKey: null, properties: '{}' },
          fields: [],
          grains: [],
          bindings: [],
          measures: [],
        }
        versions.push(v)
        return ok(summary(v))
      }
      const from = versions.find((v) => v.id === input.fromVersionId && v.datasetId === datasetId)
      if (!from) return problem(404, 'Source version not found', { fromVersionId: ['Choose a version of this dataset.'] })
      const { settings, fields, grains, bindings, measures } = from
      const v: StoredVersion = { ...base, clonedFromVersionId: from.id, ...reId(structuredClone({ settings, fields, grains, bindings, measures }), `v${versionNumber}`) }
      versions.push(v)
      return ok(summary(v))
    },
    async deleteDraft(versionId) {
      await delay()
      const found = findDraft(versionId)
      if (!found.ok) return found
      versions.splice(versions.indexOf(found.v), 1)
      return ok(null)
    },
    async validateVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      if (!found.ok) return found
      const r = report(found.v)
      if (found.v.status === 'draft' && r.violations.length === 0) found.v.status = 'validated'
      if (found.v.status === 'validated' && r.violations.length > 0) found.v.status = 'draft'
      return ok({ ...r, status: found.v.status })
    },
    async publishVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      if (!found.ok) return found
      if (found.v.status !== 'draft' && found.v.status !== 'validated') return problem(409, 'Only a draft or validated version can be published')
      const r = report(found.v)
      if (r.violations.length === 0) {
        found.v.status = 'published'
        found.v.publishedAt = new Date().toISOString()
      }
      return ok({ ...r, status: found.v.status })
    },
    async reopenVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      if (!found.ok) return found
      if (found.v.status !== 'validated') return problem(409, 'Only a validated version can return to draft', {}, { detail: 'Published versions are immutable; clone one to make changes.' })
      found.v.status = 'draft'
      return ok(summary(found.v))
    },
    async retireVersion(versionId) {
      await delay()
      const found = findVersion(versionId)
      if (!found.ok) return found
      if (found.v.status !== 'published') return problem(409, 'Only a published version can be retired')
      const active = catalog.releases.find((r) => r.isActive && r.pins.some((p) => p.versionId === versionId))
      if (active) return problem(409, 'This version is in the active release', {}, { detail: `${active.releaseKey} pins it, so the engine reads it today. Pin another version in that release first.` })
      found.v.status = 'retired'
      return ok(summary(found.v))
    },

    async saveField(versionId, input: FieldInput) {
      await delay()
      const found = findDraft(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.fields.find((f) => f.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Field not found')
      const errors: Errors = {}
      if (existing && existing.fieldKey !== input.fieldKey) err(errors, 'fieldKey', 'The field key is fixed once created.')
      else if (!ITEM_KEY.test(input.fieldKey)) err(errors, 'fieldKey', 'Start with a letter; use letters, digits and _.')
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
      const found = findDraft(versionId)
      if (!found.ok) return found
      const blocked = refuseIfBlocked(found.v, fieldId, 'field')
      if (blocked) return blocked
      found.v.fields = found.v.fields.filter((f) => f.id !== fieldId)
      return ok(null)
    },
    async addFieldsFromColumns(versionId, names) {
      await delay()
      const found = findDraft(versionId)
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
      const keys = new Set(v.fields.map((f) => f.fieldKey))
      const created = names.map((n) => {
        const c = cols.find((x) => x.name === n)!
        let key = camel(n)
        while (keys.has(key)) key = `${key}_`
        keys.add(key)
        const f: DatasetField = {
          id: newId('f'),
          fieldKey: key,
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

    async saveGrain(versionId, input: GrainInput) {
      await delay()
      const found = findDraft(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.grains.find((g) => g.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Grain not found')
      const errors: Errors = {}
      if (existing && existing.grainKey !== input.grainKey) err(errors, 'grainKey', 'The grain key is fixed once created.')
      else if (!ITEM_KEY.test(input.grainKey)) err(errors, 'grainKey', 'Start with a letter; use letters, digits and _.')
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
      const found = findDraft(versionId)
      if (!found.ok) return found
      const blocked = refuseIfBlocked(found.v, grainId, 'grain')
      if (blocked) return blocked
      found.v.grains = found.v.grains.filter((g) => g.id !== grainId)
      return ok(null)
    },

    async saveBinding(versionId, input: BindingInput) {
      await delay()
      const found = findDraft(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.bindings.find((b) => b.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Binding not found')
      const errors: Errors = {}
      const next: DatasetBinding = structuredClone({ ...input, id: existing?.id ?? newId('b') })
      checkVocab(errors, 'bindingKind', 'bindingKind', input.bindingKind)
      checkVocab(errors, 'raggedBehavior', 'raggedBehavior', input.raggedBehavior, true)
      checkVocab(errors, 'defaultPeriodAlignment', 'periodAlignment', input.defaultPeriodAlignment)
      checkVocab(errors, 'unmappedBehavior', 'unmappedBehavior', input.unmappedBehavior, true)
      const field = (id: string | null) => v.fields.find((f) => f.id === id)
      const kind = input.bindingKind
      const needsField = kind !== 'bridge'

      if (kind === 'time') {
        next.dimensionRoleId = null
        next.mappingSetId = null
        next.bridgeId = null
        next.sourceDimensionBindingId = null
        if (!input.dimensionId) err(errors, 'dimensionId', 'Choose a time dimension.')
        else if (!seedCatalog.dimensions.some((d) => d.id === input.dimensionId)) err(errors, 'dimensionId', 'Choose a dimension of this tenant.')
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
          if (src && !TIME_TYPES.has(src.dataType)) err(errors, 'sourceFieldId', `A time axis reads a date or timestamp field; '${src.fieldKey}' is ${src.dataType}.`)
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
      const found = findDraft(versionId)
      if (!found.ok) return found
      const b = found.v.bindings.find((x) => x.id === bindingId)
      if (!b) return problem(404, 'Binding not found')
      if (b.isRequired) return problem(409, 'This binding is required', {}, { detail: 'Required bindings are fixtures of the dataset and cannot be deleted.' })
      const blocked = refuseIfBlocked(found.v, bindingId, 'binding')
      if (blocked) return blocked
      found.v.bindings = found.v.bindings.filter((x) => x.id !== bindingId)
      return ok(null)
    },

    async saveMeasure(versionId, input: MeasureInput) {
      await delay()
      const found = findDraft(versionId)
      if (!found.ok) return found
      const v = found.v
      const existing = input.id ? v.measures.find((m) => m.id === input.id) : undefined
      if (input.id && !existing) return problem(404, 'Measure not found')
      const errors: Errors = {}
      const next: DatasetMeasure = structuredClone({ ...input, id: existing?.id ?? newId('m'), displayName: input.displayName.trim() })
      if (existing && existing.measureKey !== input.measureKey) err(errors, 'measureKey', 'The measure key is fixed once created.')
      else if (!ITEM_KEY.test(input.measureKey)) err(errors, 'measureKey', 'Start with a letter; use letters, digits and _.')
      if (!next.displayName) err(errors, 'displayName', 'Enter a display name.')
      checkVocab(errors, 'measureKind', 'measureKind', input.measureKind)
      const kind = input.measureKind
      const roles = vocabularies.componentRolesByKind[kind] ?? []

      if (kind === 'aggregate') {
        if (!input.aggregationFunction) err(errors, 'aggregationFunction', 'Choose how the value aggregates.')
        else checkVocab(errors, 'aggregationFunction', 'aggregationFunction', input.aggregationFunction)
      } else if (kind === 'specialized') checkVocab(errors, 'aggregationFunction', 'aggregationFunction', input.aggregationFunction, true)
      else next.aggregationFunction = null

      if (kind === 'formula') {
        next.components = []
        if (!input.expressionText?.trim()) err(errors, 'expressionText', 'Enter a formula over other measures, e.g. [m.a] / [m.b].')
        else {
          checkBrackets(errors, 'expressionText', input.expressionText)
          if (refsOf(input.expressionText, MEASURE_REF).includes(input.measureKey)) err(errors, 'expressionText', 'A formula cannot reference its own measure.')
          if (refsOf(input.expressionText, FIELD_REF).length) err(errors, 'expressionText', 'Formulas compose measures ([m.key]); fields belong in components.')
        }
      } else next.expressionText = null

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
      checkVocab(errors, 'outputDataType', 'dataType', input.outputDataType)
      checkVocab(errors, 'roundingMode', 'roundingMode', input.roundingMode)
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
      const found = findDraft(versionId)
      if (!found.ok) return found
      found.v.measures = found.v.measures.filter((m) => m.id !== measureId)
      return ok(null)
    },

    async saveSettings(versionId, s: DatasetVersionSettings) {
      await delay()
      const found = findDraft(versionId)
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
      const warnings = publicationViolations(v, catalog, (n) => columnsOf(n, v.settings.sourceObjectKind)).filter((x) => x.code === 'field_columns_exist')
      return ok({ settings: v.settings, warnings })
    },
  }
}

export type { Dependent }
