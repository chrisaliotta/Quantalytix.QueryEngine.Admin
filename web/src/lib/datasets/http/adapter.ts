import { createHttpChangeSet } from '@/lib/api/changes'
import { newTempId, unwrap, type ChangeSetPreview } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type {
  ApiResult,
  Dataset,
  DatasetField,
  DatasetListItem,
  DatasetOperation,
  DatasetsApi,
  DatasetValidationReport,
  DatasetVersion,
  DatasetVersionSummary,
  Id,
  Problem,
} from '../contract'

type Schemas = components['schemas']

const datasetPath = (id: Id) => `/datasets/${id}`
const versionPath = (id: Id) => `/dataset-versions/${id}`
const fieldPath = (versionId: Id, fieldId: Id) => `${versionPath(versionId)}/fields/${fieldId}/materialization`

/**
 * The contract's `ValidationReport` and read models are generic where this module's are narrow (dataset tabs as areas, the
 * module's vocabularies as unions); one cast each narrows them.
 */
const asReport = (report: Schemas['ValidationReport']) => report as DatasetValidationReport
const asVersion = (version: Schemas['DatasetVersion']) => version as DatasetVersion
const asListItem = (item: Schemas['DatasetListItem']) => item as DatasetListItem

const INPUT_PREFIX = /^(input|patch|settings)\./

/** Form helpers read the same error keys as the mock: `key`, not `input.key`. */
function withoutInputPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(INPUT_PREFIX, ''), messages]))
  return { ...problem, errors }
}

/** A lifecycle step's 422 is a report of the rules it broke; its success is a report with none. */
function reportOf(versionId: Id, result: ApiResult<unknown>): ApiResult<DatasetValidationReport> {
  if (result.ok) return { ok: true, data: asReport({ versionId, checkedAt: new Date().toISOString(), violations: [] }) }
  if (result.problem.status !== 422) return result
  return { ok: true, data: asReport({ versionId, checkedAt: new Date().toISOString(), violations: result.problem.violations ?? [] }) }
}

const byId = <T extends { id: Id }>(items: T[], id: Id) => items.find((item) => item.id === id)!

/** `http` is a tenant client: paths are relative to `/api/tenants/{tenantId}`. */
export function createHttpDatasetsApi(http: HttpClient): DatasetsApi {
  const changes = createHttpChangeSet<DatasetOperation, DatasetVersion>(http)
  // The change set's state is one version's content and its dataset's identity; the rest of the dataset is kept here.
  const datasets = new Map<Id, DatasetListItem>()

  function remember(item: DatasetListItem) {
    datasets.set(item.id, item)
    return item
  }

  /** A dataset as the pending change set leaves it: the pending identity, the pending version over its summary. */
  function pendingItem(item: DatasetListItem): DatasetListItem {
    const pending = changes.pending()?.state
    if (!pending || pending.datasetId !== item.id) return item
    return { ...item, ...pending.dataset, versions: item.versions.map((v) => (v.id === pending.id ? summaryOf(pending) : v)) }
  }

  const summaryOf = ({ id, datasetId, versionNumber, status, sourceObject, createdAt, publishedAt, clonedFromVersionId, releases }: DatasetVersion): DatasetVersionSummary => ({
    id,
    datasetId,
    versionNumber,
    status,
    sourceObject,
    createdAt,
    publishedAt,
    clonedFromVersionId,
    releases,
  })

  async function getVersion(versionId: Id): Promise<ApiResult<DatasetVersion>> {
    const pending = changes.pending()?.state
    if (pending?.id === versionId) return { ok: true, data: pending }
    const result = await http.get<Schemas['DatasetVersion']>(versionPath(versionId))
    return result.ok ? { ok: true, data: asVersion(result.data) } : result
  }

  const versionOf = (versionId: Id) => unwrap(getVersion(versionId))

  async function stage(versionId: Id, op: DatasetOperation): Promise<ApiResult<ChangeSetPreview<DatasetVersion>>> {
    const result = await changes.stage(versionPath(versionId), op satisfies Schemas['DatasetOperation'])
    return result.ok ? result : { ok: false, problem: withoutInputPrefix(result.problem) }
  }

  async function edit<T>(versionId: Id, op: DatasetOperation, answer: (preview: ChangeSetPreview<DatasetVersion>) => T): Promise<ApiResult<T>> {
    const result = await stage(versionId, op)
    return result.ok ? { ok: true, data: answer(result.data) } : result
  }

  /** The version an identity edit stages on: the open change set's, else the dataset's draft, else its newest version. */
  function editableVersionOf(datasetId: Id): Id {
    const pending = changes.pending()?.state
    if (pending?.datasetId === datasetId) return pending.id
    const versions = datasets.get(datasetId)!.versions
    return (versions.find((v) => v.status === 'draft') ?? versions[0]).id
  }

  return {
    ...changes,

    listSourceObjects: () => unwrap(http.get<Schemas['DatasetSourceObject'][]>('/source-objects')),
    getSourceColumns: (kind, name) => http.get<Schemas['DatasetSourceColumn'][]>(`/source-objects/${kind}/${encodeURIComponent(name)}/columns`),
    async listDatasets() {
      const items = (await unwrap(http.get<Schemas['DatasetListItem'][]>('/datasets'))).map((item) => remember(asListItem(item)))
      return items.map(pendingItem)
    },
    async getDataset(datasetId) {
      const result = await http.get<Schemas['DatasetListItem']>(datasetPath(datasetId))
      return result.ok ? { ok: true, data: pendingItem(remember(asListItem(result.data))) } : result
    },
    getVersion,
    async checkVersion(versionId) {
      const result = await http.post<Schemas['ValidationReport']>(`${versionPath(versionId)}/check`)
      return result.ok ? { ok: true, data: asReport(result.data) } : result
    },
    checkDeleteDataset: (datasetId) => unwrap(http.get<Schemas['DeleteCheck']>(`${datasetPath(datasetId)}/delete-check`)),
    checkDeleteVersion: (versionId) => unwrap(http.get<Schemas['DeleteCheck']>(`${versionPath(versionId)}/delete-check`)),

    updateDataset: (datasetId, patch) => edit(editableVersionOf(datasetId), { op: 'updateDataset', patch }, ({ state }): Dataset => state.dataset),
    saveSettings: (versionId, settings) =>
      edit(versionId, { op: 'saveSettings', settings }, ({ state, violations }) => ({
        settings: state.settings,
        warnings: violations.filter((v) => v.code === 'field_columns_exist') as DatasetValidationReport['violations'],
      })),
    renameSourceTable: (versionId, tableName) => edit(versionId, { op: 'renameSourceTable', tableName }, ({ state }) => state.settings),
    saveField(versionId, input) {
      const field = { ...input, id: input.id ?? newTempId() }
      return edit(versionId, { op: 'saveField', input: field }, ({ state }) => byId(state.fields, field.id))
    },
    deleteField: (versionId, fieldId) => edit(versionId, { op: 'deleteField', fieldId }, () => null),
    async addFieldsFromColumns(versionId, columns) {
      const before = new Set((await versionOf(versionId)).fields.map((f) => f.id))
      return edit(versionId, { op: 'addFieldsFromColumns', columns }, ({ state }) => state.fields.filter((f) => !before.has(f.id)))
    },
    saveGrain(versionId, input) {
      const grain = { ...input, id: input.id ?? newTempId() }
      return edit(versionId, { op: 'saveGrain', input: grain }, ({ state }) => byId(state.grains, grain.id))
    },
    deleteGrain: (versionId, grainId) => edit(versionId, { op: 'deleteGrain', grainId }, () => null),
    saveBinding(versionId, input) {
      const binding = { ...input, id: input.id ?? newTempId() }
      return edit(versionId, { op: 'saveBinding', input: binding }, ({ state }) => byId(state.bindings, binding.id))
    },
    deleteBinding: (versionId, bindingId) => edit(versionId, { op: 'deleteBinding', bindingId }, () => null),
    saveMeasure(versionId, input) {
      const measure = { ...input, id: input.id ?? newTempId() }
      return edit(versionId, { op: 'saveMeasure', input: measure }, ({ state }) => byId(state.measures, measure.id))
    },
    deleteMeasure: (versionId, measureId) => edit(versionId, { op: 'deleteMeasure', measureId }, () => null),

    async createDataset(input) {
      const result = await http.post<Schemas['DatasetListItem']>('/datasets', input satisfies Schemas['DatasetCreateInput'])
      return result.ok ? { ok: true, data: remember(asListItem(result.data)) } : result
    },
    deleteDataset: (datasetId) => http.delete(datasetPath(datasetId)),
    createDraft: (datasetId, input) => http.post<Schemas['DatasetVersionSummary']>(`${datasetPath(datasetId)}/versions`, input satisfies Schemas['DatasetCreateDraftInput']),
    deleteDraft: (versionId) => http.delete(versionPath(versionId)),
    validateVersion: async (versionId) => reportOf(versionId, await http.post(`${versionPath(versionId)}/validate`)),
    publishVersion: async (versionId) => reportOf(versionId, await http.post(`${versionPath(versionId)}/publish`)),
    reopenVersion: (versionId) => http.post<Schemas['DatasetVersionSummary']>(`${versionPath(versionId)}/reopen`),
    retireVersion: (versionId) => http.post<Schemas['DatasetVersionSummary']>(`${versionPath(versionId)}/retire`),
    materializeField: (versionId, fieldId) => http.post<DatasetField>(fieldPath(versionId, fieldId)),
    refreshMaterialization: (versionId, fieldId) => http.post<DatasetField>(`${fieldPath(versionId, fieldId)}/refresh`),
    dematerializeField: (versionId, fieldId) => http.delete<DatasetField>(fieldPath(versionId, fieldId)),
  }
}
