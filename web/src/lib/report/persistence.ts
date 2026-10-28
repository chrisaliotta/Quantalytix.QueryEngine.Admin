import { toast } from 'sonner'
import { unwrap, type ChangeSetCommit, type Id, type Problem } from '@/lib/api/contract'
import { apiMode, tenantHttp } from '@/lib/api/session'
import { modelPersistence, modelIdOf, modelKeyOf } from '@/lib/model/persistence'
import { refusalOf, sharedReportsApi } from '@/lib/reports/client'
import type { ReportDocument, ReportOperation } from '@/lib/reports/contract'
import { loadReportCatalog, type ReportCatalog } from './http-catalog'
import { clientOnlyChanges, reportFromDocument, reportOperations, type BridgeContext } from './http-bridge'
import { sampleReports } from './mockData'
import type { ReportState } from './types'

export interface StoredReport {
  id: string
  report: ReportState
}

export type Stored<T> = { ok: true; value: T } | { ok: false; messages: string[] }

/** Where the report editor keeps its library: in memory (mock mode) or the Admin API (http mode). */
export interface ReportPersistence {
  reports: StoredReport[]
  /** The row the editor selects when it opens. */
  initialRowKey: string | null
  /** Stores `next` over the saved `prev`; resolves with the refusal messages, if any. */
  save(id: string, prev: ReportState, next: ReportState): Promise<string[] | null>
  /** Stores `copy`, a copy of report `fromId` under a new key and name; resolves with the report as stored. */
  copy(id: string, fromId: string, copy: ReportState): Promise<Stored<ReportState>>
  remove(id: string): Promise<string[] | null>
  /** The id the mappings module names the report's bridges' owner by. */
  bridgeOwnerId(id: string, report: ReportState): Id
}

const memory: ReportPersistence = {
  reports: sampleReports.map((report, i) => ({ id: `report_${i + 1}`, report })),
  initialRowKey: 'LOANS',
  save: async () => null,
  copy: async (_id, _fromId, copy) => ({ ok: true, value: copy }),
  remove: async () => null,
  bridgeOwnerId: (_id, report) => report.settings.key,
}

const messagesOf = (problem: Problem) => refusalOf(problem).map((v) => v.message)

let catalog: ReportCatalog | null = null
/** The stored document of each report, by the editor's id; a report created in this session has its own id until reload. */
const stored = new Map<string, ReportDocument>()
let loaded: StoredReport[] = []

function contextOf(doc: ReportDocument, unmapped: BridgeContext['unmapped'] = {}): BridgeContext {
  const modelKey = modelKeyOf(doc.modelId) ?? doc.modelId
  return {
    catalog: catalog!,
    model: modelPersistence().models.find((m) => m.key === modelKey),
    modelKey: (id) => modelKeyOf(id) ?? id,
    modelId: (key) => modelIdOf(key) ?? key,
    unmapped,
  }
}

/** The report as the editor holds it, with the server's coverage of its direct sources. */
async function fromServer(doc: ReportDocument) {
  const coverage = (await sharedReportsApi().getDiagnostics(doc.id)).coverage
  const unmapped = Object.fromEntries(doc.sources.filter((s) => s.method === 'direct').map((s) => [s.key, coverage[s.key]?.unmapped ?? []]))
  return reportFromDocument(doc, contextOf(doc, unmapped))
}

/** Reads the catalog and every report before the app renders (http mode); call after the models are loaded. */
export async function loadReports() {
  catalog = await loadReportCatalog()
  const api = sharedReportsApi()
  const docs = await Promise.all((await api.listReports()).map((r) => unwrap(api.getReport(r.id))))
  for (const doc of docs) stored.set(doc.id, doc)
  loaded = await Promise.all(docs.map(async (doc) => ({ id: doc.id, report: await fromServer(doc) })))
}

const http: Omit<ReportPersistence, 'reports' | 'initialRowKey'> = {
  async save(id, prev, next) {
    const doc = stored.get(id)!
    const operations = reportOperations(doc, prev, next, contextOf(doc))
    if (operations.length) {
      const commit = await tenantHttp.post<ChangeSetCommit<ReportDocument>>(`/reports/${doc.id}/changes`, { operations } satisfies { operations: ReportOperation[] })
      if (!commit.ok) return messagesOf(commit.problem)
      stored.set(id, commit.data.state)
    }
    if (prev.enabled !== next.enabled) {
      const enabled = await sharedReportsApi().setEnabled(doc.id, next.enabled)
      if (!enabled.ok) return messagesOf(enabled.problem)
      stored.set(id, { ...stored.get(id)!, isEnabled: next.enabled })
    }
    const kept = clientOnlyChanges(prev, next)
    if (kept.length) toast.warning(`The ${kept.join(', ')} change is kept in this browser only`, { description: 'The Admin API has no field for it, so it is not saved.' })
    return null
  },
  async copy(id, fromId, copy) {
    const from = stored.get(fromId)!
    const api = sharedReportsApi()
    const created = await api.createReport({
      key: copy.settings.key,
      displayName: copy.settings.name,
      reportType: copy.settings.type,
      modelId: from.modelId,
      copyFromReportId: from.id,
    })
    if (!created.ok) return { ok: false, messages: messagesOf(created.problem) }
    const doc = await unwrap(api.getReport(created.data.id))
    stored.set(id, doc)
    return { ok: true, value: await fromServer(doc) }
  },
  async remove(id) {
    const result = await sharedReportsApi().deleteReport(stored.get(id)!.id)
    if (!result.ok) return messagesOf(result.problem)
    stored.delete(id)
    return null
  },
  bridgeOwnerId: (id) => stored.get(id)!.id,
}

export function reportPersistence(): ReportPersistence {
  return apiMode === 'http' ? { reports: loaded, initialRowKey: loaded[0].report.rows[0]?.key ?? null, ...http } : memory
}
