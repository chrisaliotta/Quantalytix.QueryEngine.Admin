import { createHttpChangeSet } from '@/lib/api/changes'
import { newTempId, unwrap, type FreezeInput } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type {
  ApiResult,
  DefinitionOrigin,
  DeleteCheck,
  Dimension,
  DimensionCreated,
  DimensionDeleteTarget,
  DimensionDetail,
  DimensionEditState,
  DimensionListItem,
  DimensionOperation,
  DimensionsApi,
  DimensionValidationReport,
  DimensionVersionContent,
  Id,
  Problem,
} from '../contract'

type Schemas = components['schemas']

const dimensionPath = (id: Id) => `/dimensions/${id}`
const versionPath = (id: Id) => `/dimension-versions/${id}`

function deleteCheckPath(target: DimensionDeleteTarget) {
  switch (target.type) {
    case 'dimension':
      return `${dimensionPath(target.dimensionId)}/delete-check`
    case 'version':
      return `${versionPath(target.versionId)}/delete-check`
    case 'member':
      return `${versionPath(target.versionId)}/members/${target.memberId}/delete-check`
    case 'level':
      return `${versionPath(target.versionId)}/levels/${target.levelId}/delete-check`
    case 'role':
      return `/dimension-roles/${target.roleId}/delete-check`
  }
}

/** The contract's `ValidationReport` and `DeleteBlocker` are generic; this module's codes and areas narrow them. */
const asReport = (report: Schemas['ValidationReport']) => report as DimensionValidationReport
const asDeleteCheck = (check: Schemas['DeleteCheck']) => check as DeleteCheck

const INPUT_PREFIX = /^(input|patch)\./

/** Form helpers read the same error keys as the mock: `key`, not `input.key`. */
function withoutInputPrefix(problem: Problem): Problem {
  const errors = Object.fromEntries(Object.entries(problem.errors).map(([path, messages]) => [path.replace(INPUT_PREFIX, ''), messages]))
  return { ...problem, errors }
}

function dimensionFields({ id, key, displayName, kind, origin, purpose, ownerReport, profileKey, isEnabled, isRemovable }: Dimension): Dimension {
  return { id, key, displayName, kind, origin, purpose, ownerReport, profileKey, isEnabled, isRemovable }
}

const byId = <T extends { id: Id }>(items: T[], id: Id) => items.find((item) => item.id === id)!

/** `http` is a tenant client: paths are relative to `/api/tenants/{tenantId}`. */
export function createHttpDimensionsApi(http: HttpClient): DimensionsApi {
  const changes = createHttpChangeSet<DimensionOperation, DimensionEditState>(http)
  // The change set is staged on the dimension, so version and role edits look up their dimension here.
  const versionDimension = new Map<Id, Id>()
  const roleDimension = new Map<Id, Id>()

  function rememberDimension(item: DimensionListItem) {
    for (const v of item.versions) versionDimension.set(v.id, item.id)
    for (const r of item.roles) roleDimension.set(r.id, item.id)
  }
  const rememberContent = ({ version }: DimensionVersionContent) => versionDimension.set(version.id, version.dimensionId)

  function rememberCreated(result: ApiResult<Schemas['DimensionCreated']>): ApiResult<DimensionCreated> {
    if (result.ok) versionDimension.set(result.data.versionId, result.data.dimensionId)
    return result
  }

  async function listDimensions(origin: DefinitionOrigin): Promise<DimensionListItem[]> {
    const items = await unwrap(http.get<Schemas['DimensionListItem'][]>('/dimensions', { origin }))
    items.forEach(rememberDimension)
    return items
  }

  async function getDimension(id: Id): Promise<ApiResult<DimensionDetail>> {
    const pending = changes.pending()?.state.dimension
    if (pending?.id === id) return { ok: true, data: pending }
    const result = await http.get<Schemas['DimensionDetail']>(dimensionPath(id))
    if (result.ok) rememberDimension(result.data)
    return result
  }

  async function getVersion(versionId: Id): Promise<ApiResult<DimensionVersionContent>> {
    const pending = changes.pending()?.state.content
    if (pending?.version.id === versionId) return { ok: true, data: pending }
    const result = await http.get<Schemas['DimensionVersionContent']>(versionPath(versionId))
    if (result.ok) rememberContent(result.data)
    return result
  }

  const contentOf = (versionId: Id) => unwrap(getVersion(versionId))

  async function edit<T>(dimensionId: Id, op: DimensionOperation, answer: (state: DimensionEditState) => T): Promise<ApiResult<T>> {
    const result = await changes.stage(dimensionPath(dimensionId), op satisfies Schemas['DimensionOperation'])
    if (!result.ok) return { ok: false, problem: withoutInputPrefix(result.problem) }
    rememberDimension(result.data.state.dimension)
    rememberContent(result.data.state.content)
    return { ok: true, data: answer(result.data.state) }
  }

  const editVersion = <T>(op: Extract<DimensionOperation, { versionId: Id }>, answer: (state: DimensionEditState) => T) =>
    edit(versionDimension.get(op.versionId)!, op, answer)

  return {
    ...changes,
    listDimensions,
    async listAllDimensions() {
      const [user, system] = await Promise.all([listDimensions('user'), listDimensions('system')])
      return [...user, ...system]
    },
    listAvailableProfiles: () => unwrap(http.get<Schemas['AvailableDimensionProfile'][]>('/dimension-profiles', { available: true })),
    getDimension,
    getVersion,
    async checkVersion(versionId) {
      const result = await http.post<Schemas['ValidationReport']>(`${versionPath(versionId)}/check`)
      return result.ok ? { ok: true, data: asReport(result.data) } : result
    },
    checkDelete: async (target) => asDeleteCheck(await unwrap(http.get<Schemas['DeleteCheck']>(deleteCheckPath(target)))),

    updateDimension: (id, patch) => edit(id, { op: 'updateDimension', patch }, (s) => dimensionFields(s.dimension)),
    updateVersion: (versionId, patch) => editVersion({ op: 'updateVersion', versionId, patch }, (s) => s.content.version),
    saveMember(versionId, input) {
      const member = { ...input, id: input.id ?? newTempId() }
      return editVersion({ op: 'saveMember', versionId, input: member }, (s) => byId(s.content.members, member.id))
    },
    deleteMember: (versionId, memberId) => editVersion({ op: 'deleteMember', versionId, memberId }, () => null),
    async moveMember(versionId, memberId, to) {
      const { members } = await contentOf(versionId)
      const member = byId(members, memberId)
      const siblings = members.filter((m) => m.parentId === member.parentId).sort((a, b) => a.sortOrder - b.sortOrder)
      const previous = { parentId: member.parentId, index: siblings.indexOf(member) }
      return editVersion({ op: 'moveMember', versionId, memberId, to }, () => previous)
    },
    async importMembers(versionId, rows, mode = 'merge') {
      const before = new Set((await contentOf(versionId)).members.map((m) => m.key))
      const imported = new Set(rows.map((r) => r.key))
      return editVersion({ op: 'importMembers', versionId, rows, mode }, ({ content }) => {
        const after = new Set(content.members.map((m) => m.key))
        return {
          added: content.members.filter((m) => !before.has(m.key)),
          updated: content.members.filter((m) => before.has(m.key) && imported.has(m.key)),
          removed: [...before].filter((key) => !after.has(key)).length,
        }
      })
    },
    async clearMembers(versionId) {
      const { members } = await contentOf(versionId)
      return editVersion({ op: 'clearMembers', versionId }, () => ({ removed: members.length }))
    },
    saveLevel(versionId, input) {
      const level = { ...input, id: input.id ?? newTempId() }
      return editVersion({ op: 'saveLevel', versionId, input: level }, (s) => byId(s.content.levels, level.id))
    },
    deleteLevel: (versionId, levelId) => editVersion({ op: 'deleteLevel', versionId, levelId }, () => null),
    reorderLevels: (versionId, orderedIds) =>
      editVersion({ op: 'reorderLevels', versionId, orderedIds }, (s) => [...s.content.levels].sort((a, b) => a.ordinal - b.ordinal)),
    saveBands: (versionId, bands) => editVersion({ op: 'saveBands', versionId, bands }, (s) => s.content.bands),
    saveRole(dimensionId, input) {
      const role = { ...input, id: input.id ?? newTempId() }
      return edit(dimensionId, { op: 'saveRole', input: role }, (s) => byId(s.dimension.roles, role.id))
    },
    deleteRole: (roleId) => edit(roleDimension.get(roleId)!, { op: 'deleteRole', roleId }, () => null),

    createDimension: async (input) =>
      rememberCreated(await http.post<Schemas['DimensionCreated']>('/dimensions', input satisfies Schemas['CreateDimensionInput'])),
    defineSystemDimension: async (profileKey) =>
      rememberCreated(await http.post<Schemas['DimensionCreated']>(`/dimension-profiles/${encodeURIComponent(profileKey)}/define`)),
    deleteDimension: (id) => http.delete(dimensionPath(id)),
    async freezeVersion(versionId, note) {
      const result = await http.post<Schemas['DimensionFreezeResult']>(`${versionPath(versionId)}/freeze`, { note } satisfies FreezeInput)
      if (result.ok) return { ok: true, data: { report: asReport(result.data.report), live: result.data.live } }
      if (result.problem.status !== 422) return result
      const report = { versionId, checkedAt: new Date().toISOString(), violations: result.problem.violations ?? [] }
      return { ok: true, data: { report: asReport(report), live: null } }
    },
    deleteVersion: (versionId) => http.delete(versionPath(versionId)),
  }
}
