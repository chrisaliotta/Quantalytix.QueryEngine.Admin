import { unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { Id, SnapshotsApi } from '../contract'

type Schemas = components['schemas']

const snapshotPath = (id: Id) => `/snapshots/${id}`

/** `SnapshotsApi` over the Admin API; `http` is a tenant client (paths relative to `/api/tenants/{tenantId}`). */
export function createHttpSnapshotsApi(http: HttpClient): SnapshotsApi {
  return {
    listSnapshots: () => unwrap(http.get<Schemas['SnapshotList']>('/snapshots')),
    getSnapshot: (id) => http.get<Schemas['SnapshotDetail']>(snapshotPath(id)),
    compareSnapshots: (fromId, toId) => unwrap(http.get<Schemas['SnapshotComparison']>('/snapshots/compare', { from: fromId, to: toId })),
    capturePreview: () => unwrap(http.get<Schemas['SnapshotCounts']>('/snapshots/capture-preview')),

    captureSnapshot: (input) => http.post<Schemas['Snapshot']>('/snapshots', input satisfies Schemas['CaptureSnapshotInput']),
    restoreSnapshot: (id) => http.post<Schemas['TenantInfo']>(`${snapshotPath(id)}/restore`),
    relabelSnapshot: (id, input) => http.put<Schemas['Snapshot']>(snapshotPath(id), input satisfies Schemas['RelabelSnapshotInput']),
    deleteSnapshot: (id) => http.delete(snapshotPath(id)),
  }
}
