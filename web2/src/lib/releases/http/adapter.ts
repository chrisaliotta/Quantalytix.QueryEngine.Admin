import { createHttpChangeSet } from '@/lib/api/changes'
import { unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { TenantInfo } from '@/lib/tenant/contract'
import type {
  ApiResult,
  AssumptionSet,
  AssumptionSetList,
  Id,
  ManifestDataset,
  PinIdentity,
  Release,
  ReleaseChangeSetPreview,
  ReleaseCheckReport,
  ReleaseDetail,
  ReleaseDiff,
  ReleaseListItem,
  ReleaseOperation,
  ReleaseReportRef,
  ReleasesApi,
  ReleaseTransitionResult,
  SetCapturePreview,
  SetComparison,
  SetDetail,
} from '../contract'

type Schemas = components['schemas']

const releasePath = (id: Id) => `/releases/${id}`
const setPath = (id: Id) => `/assumption-sets/${id}`

/**
 * The contract's generated types are looser than the port's where this module narrows them (rule codes, target areas, pin kinds);
 * one cast each narrows them.
 */
const asReport = (report: Schemas['ReleaseCheckReport']) => report as ReleaseCheckReport
const asDetail = (detail: Schemas['ReleaseDetail']) => detail as ReleaseDetail

/** `http` is a tenant client: paths are relative to `/api/tenants/{tenantId}`. */
export function createHttpReleasesApi(http: HttpClient): ReleasesApi {
  const changes = createHttpChangeSet<ReleaseOperation, ReleaseDetail>(http)

  /** The open manifest change set's preview, which carries the pending report and diff. */
  const pending = () => changes.pending() as ReleaseChangeSetPreview | null

  /** A list item as the pending manifest leaves it. */
  function pendingItem(item: ReleaseListItem): ReleaseListItem {
    const preview = pending()
    if (preview?.state.release.id !== item.id) return item
    return { ...item, datasetCount: preview.state.manifest.length, diffVsActive: preview.diffVsActive }
  }

  async function getRelease(id: Id): Promise<ApiResult<ReleaseDetail>> {
    const preview = pending()
    if (preview?.state.release.id === id) return { ok: true, data: preview.state }
    const result = await http.get<Schemas['ReleaseDetail']>(releasePath(id))
    return result.ok ? { ok: true, data: asDetail(result.data) } : result
  }

  const checkStored = async (id: Id) => asReport(await unwrap(http.post<Schemas['ReleaseCheckReport']>(`${releasePath(id)}/check`)))

  /** Validate and publish answer a failed rule run as data, as the mock does: the release unchanged and the stored manifest's report. */
  async function transition(id: Id, step: 'validate' | 'publish'): Promise<ApiResult<ReleaseTransitionResult>> {
    const result = await http.post<Schemas['ReleaseTransitionResult']>(`${releasePath(id)}/${step}`)
    if (result.ok) return { ok: true, data: { release: result.data.release as Release, report: asReport(result.data.report) } }
    if (result.problem.status !== 422) return result
    const detail = await unwrap(getRelease(id))
    return { ok: true, data: { release: detail.release, report: await checkStored(id) } }
  }

  return {
    ...changes,

    async listReleases() {
      const items = await unwrap(http.get<Schemas['ReleaseListItem'][]>('/releases'))
      return (items as ReleaseListItem[]).map(pendingItem)
    },
    getRelease,
    diffReleases: async (fromId, toId) => (await unwrap(http.get<Schemas['ReleaseDiff']>('/releases/diff', { from: fromId, to: toId }))) as ReleaseDiff,
    async checkRelease(id) {
      const preview = pending()
      return preview?.state.release.id === id ? preview.report : checkStored(id)
    },
    listManifestDatasets: async () => (await unwrap(http.get<Schemas['ManifestDataset'][]>('/manifest-datasets'))) as ManifestDataset[],
    listIdentities: async () => (await unwrap(http.get<Schemas['PinIdentity'][]>('/identities'))) as PinIdentity[],
    listReportReads: async () => (await unwrap(http.get<Schemas['ReleaseReportRef'][]>('/report-reads'))) as ReleaseReportRef[],
    listSets: async () => (await unwrap(http.get<Schemas['AssumptionSetList']>('/assumption-sets'))) as AssumptionSetList,
    async getSet(id) {
      const result = await http.get<Schemas['SetDetail']>(setPath(id))
      return result.ok ? { ok: true, data: result.data as SetDetail } : result
    },
    compareSets: async (fromSetId, toSetId) =>
      (await unwrap(http.get<Schemas['SetComparison']>('/assumption-sets/compare', { from: fromSetId, to: toSetId }))) as SetComparison,
    capturePreview: async () => (await unwrap(http.get<Schemas['SetCapturePreview']>('/assumption-sets/capture-preview'))) as SetCapturePreview,

    async saveManifest(id, manifest) {
      const result = await changes.stage(releasePath(id), { op: 'saveManifest', manifest })
      return result.ok ? { ok: true, data: result.data.state } : result
    },

    createRelease: (input) => http.post<Release>('/releases', input satisfies Schemas['CreateReleaseInput']),
    validateRelease: (id) => transition(id, 'validate'),
    publishRelease: (id) => transition(id, 'publish'),
    backToDraft: (id) => http.post<Release>(`${releasePath(id)}/reopen`),
    retireRelease: (id) => http.post<Release>(`${releasePath(id)}/retire`),
    activateRelease: (id) => http.post<TenantInfo>(`${releasePath(id)}/activate`),
    deleteRelease: (id) => http.delete(releasePath(id)),
    captureSet: (input) => http.post<AssumptionSet>('/assumption-sets', input satisfies Schemas['CaptureSetInput']),
    relabelSet: (id, input) => http.put<AssumptionSet>(setPath(id), input satisfies Schemas['RelabelSetInput']),
    deleteSet: (id) => http.delete(setPath(id)),
  }
}
