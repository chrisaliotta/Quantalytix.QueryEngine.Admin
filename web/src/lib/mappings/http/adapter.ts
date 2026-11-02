import { createHttpChangeSet } from '@/lib/api/changes'
import { unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type {
  ApiResult,
  Bridge,
  BridgeEditState,
  BridgeOperation,
  Id,
  MappingOperation,
  MappingSet,
  MappingSetEditState,
  MappingsApi,
  Problem,
} from '../contract'

type Schemas = components['schemas']
type EditState = MappingSetEditState | BridgeEditState

const setPath = (id: Id) => `/mapping-sets/${id}`
const mappingVersionPath = (id: Id) => `/mapping-versions/${id}`
const bridgePath = (id: Id) => `/bridges/${id}`
const bridgeVersionPath = (id: Id) => `/bridge-versions/${id}`

const INPUT_PREFIX = /^(input|patch)\./

/** Form helpers read the same error keys as the mock: `memberKey`, not `input.memberKey`. */
function withoutInputPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(INPUT_PREFIX, ''), messages]))
  return { ...problem, errors }
}

const isSetState = (state: EditState): state is MappingSetEditState => 'set' in state
const byId = <T extends { id: Id }>(items: T[], id: Id) => items.find((item) => item.id === id)!

function setFields({ id, key, displayName, sourceNamespace, dimensionId }: MappingSet): MappingSet {
  return { id, key, displayName, sourceNamespace, dimensionId }
}

function bridgeFields({ id, key, displayName, sourceDimensionId, targetDimensionId, isEnabled, owner }: Bridge): Bridge {
  return { id, key, displayName, sourceDimensionId, targetDimensionId, isEnabled, owner }
}

/**
 * `MappingsApi` over the Admin API; `http` is a tenant client (paths relative to `/api/tenants/{tenantId}`). One change set covers
 * one mapping set or one analytical bridge, so edits look up the identity of the version they name, which every read remembers.
 */
export function createHttpMappingsApi(http: HttpClient): MappingsApi {
  const changes = createHttpChangeSet<MappingOperation | BridgeOperation, EditState>(http)
  const versionSet = new Map<Id, Id>()
  const versionBridge = new Map<Id, Id>()

  const rememberSet = (set: { id: Id; versions: { id: Id }[] }) => set.versions.forEach((v) => versionSet.set(v.id, set.id))
  const rememberBridge = (bridge: { id: Id; versions: { id: Id }[] }) => bridge.versions.forEach((v) => versionBridge.set(v.id, bridge.id))

  function pending<S extends EditState>(is: (state: EditState) => state is S): S | null {
    const state = changes.pending()?.state
    return state && is(state) ? state : null
  }
  const pendingSet = () => pending(isSetState)
  const pendingBridge = () => pending((state): state is BridgeEditState => !isSetState(state))

  async function edit<S extends EditState, T>(path: string, op: MappingOperation | BridgeOperation, answer: (state: S) => T): Promise<ApiResult<T>> {
    const result = await changes.stage(path, op)
    if (!result.ok) return { ok: false, problem: withoutInputPrefix(result.problem) }
    return { ok: true, data: answer(result.data.state as S) }
  }

  const editSet = <T>(id: Id, op: MappingOperation, answer: (state: MappingSetEditState) => T) => edit(setPath(id), op satisfies Schemas['MappingOperation'], answer)
  const editMappingVersion = <T>(op: Extract<MappingOperation, { versionId: Id }>, answer: (state: MappingSetEditState) => T) =>
    editSet(versionSet.get(op.versionId)!, op, answer)
  const editBridge = <T>(id: Id, op: BridgeOperation, answer: (state: BridgeEditState) => T) => edit(bridgePath(id), op satisfies Schemas['BridgeOperation'], answer)
  const editBridgeVersion = <T>(op: Extract<BridgeOperation, { versionId: Id }>, answer: (state: BridgeEditState) => T) =>
    editBridge(versionBridge.get(op.versionId)!, op, answer)

  return {
    ...changes,

    async listMappingSets(filter) {
      const items = await unwrap(http.get<Schemas['MappingSetListItem'][]>('/mapping-sets', { dimensionId: filter?.dimensionId }))
      items.forEach(rememberSet)
      return items
    },
    async getMappingSet(id) {
      const staged = pendingSet()?.set
      if (staged?.id === id) return { ok: true, data: staged }
      const result = await http.get<Schemas['MappingSetListItem']>(setPath(id))
      if (result.ok) rememberSet(result.data)
      return result
    },
    async getMappingVersion(versionId) {
      const staged = pendingSet()?.content
      if (staged?.version.id === versionId) return { ok: true, data: staged }
      const result = await http.get<Schemas['MappingVersionContent']>(mappingVersionPath(versionId))
      if (result.ok) versionSet.set(versionId, result.data.version.mappingSetId)
      return result
    },
    checkDeleteMappingSet: (id) => unwrap(http.get<Schemas['DeleteCheck']>(`${setPath(id)}/delete-check`)),

    async listBridges(filter) {
      const items = await unwrap(http.get<Schemas['BridgeListItem'][]>('/bridges', { dimensionId: filter?.dimensionId }))
      items.forEach(rememberBridge)
      return items
    },
    async getBridge(id) {
      const staged = pendingBridge()?.bridge
      if (staged?.id === id) return { ok: true, data: staged }
      const result = await http.get<Schemas['BridgeListItem']>(bridgePath(id))
      if (result.ok) rememberBridge(result.data)
      return result
    },
    async findReportBridge(reportId, sourceKey) {
      const [bridge] = await unwrap(http.get<Schemas['BridgeListItem'][]>('/bridges', { reportId, sourceKey }))
      return bridge ?? null
    },
    async getBridgeVersion(versionId) {
      const staged = pendingBridge()?.content
      if (staged?.version.id === versionId) return { ok: true, data: staged }
      const result = await http.get<Schemas['BridgeVersionContent']>(bridgeVersionPath(versionId))
      if (result.ok) versionBridge.set(versionId, result.data.version.bridgeId)
      return result
    },
    checkDeleteBridge: (id) => unwrap(http.get<Schemas['DeleteCheck']>(`${bridgePath(id)}/delete-check`)),

    updateMappingSet: (id, patch) => editSet(id, { op: 'updateMappingSet', patch }, (s) => setFields(s.set)),
    saveEntry: (versionId, input) => editMappingVersion({ op: 'saveEntry', versionId, input }, (s) => byId(s.content.entries, input.id)),
    deleteEntry: (versionId, entryId) => editMappingVersion({ op: 'deleteEntry', versionId, entryId }, () => null),
    replaceEntries: (versionId, entries) => editMappingVersion({ op: 'replaceEntries', versionId, entries }, (s) => s.content.entries),

    updateBridge: (id, patch) => editBridge(id, { op: 'updateBridge', patch }, (s) => bridgeFields(s.bridge)),
    updateBridgeVersion: (versionId, patch) => editBridgeVersion({ op: 'updateBridgeVersion', versionId, patch }, (s) => s.content.version),
    saveBridgeMapping: (versionId, input) => editBridgeVersion({ op: 'saveBridgeMapping', versionId, input }, (s) => byId(s.content.mappings, input.id)),
    deleteBridgeMapping: (versionId, mappingId) => editBridgeVersion({ op: 'deleteBridgeMapping', versionId, mappingId }, () => null),
    replaceBridgeMappings: (versionId, rows) => editBridgeVersion({ op: 'replaceBridgeMappings', versionId, rows }, (s) => s.content.mappings),

    async createMappingSet(input) {
      const result = await http.post<Schemas['MappingSetCreated']>('/mapping-sets', input satisfies Schemas['CreateMappingSetInput'])
      if (result.ok) versionSet.set(result.data.versionId, result.data.mappingSetId)
      return result
    },
    deleteMappingSet: (id) => http.delete(setPath(id)),
    async createBridge(input) {
      const result = await http.post<Schemas['BridgeCreated']>('/bridges', input satisfies Schemas['CreateBridgeInput'])
      if (result.ok) versionBridge.set(result.data.versionId, result.data.bridgeId)
      return result
    },
    deleteBridge: (id) => http.delete(bridgePath(id)),
  }
}
