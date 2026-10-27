import type {
  ManifestEntry,
  PinIdentity,
  PinKind,
  PinRow,
  ReleaseCheckReport,
  ReleaseDiff,
  ReleaseReportRef,
  ReleaseRuleCode,
  ReleaseViolation,
  SetReadiness,
} from '../contract'
import type { Db } from './data'

export const publishedVersionOf = (i: PinIdentity) => i.versions.find((v) => v.status === 'published') ?? null

export function manifestOf(db: Db, releaseId: string): ManifestEntry[] {
  return db.manifests.filter((m) => m.releaseId === releaseId).map(({ datasetId, datasetVersionId }) => ({ datasetId, datasetVersionId }))
}

export function datasetVersionOf(db: Db, versionId: string) {
  for (const d of db.datasets) {
    const v = d.versions.find((x) => x.id === versionId)
    if (v) return { ...v, datasetId: d.id }
  }
  return undefined
}

/** FNV-1a over the sorted manifest; the API's hash is its own. */
export function manifestHash(manifest: ManifestEntry[]) {
  const text = manifest
    .map((m) => `${m.datasetId}:${m.datasetVersionId}`)
    .sort()
    .join('\n')
  let h = 0x811c9dc5
  for (let i = 0; i < text.length; i++) h = Math.imul(h ^ text.charCodeAt(i), 0x01000193)
  return (h >>> 0).toString(16).padStart(8, '0')
}

const KIND_WORD: Record<PinKind, string> = { dimension: 'dimension', mapping_set: 'mapping set', bridge: 'bridge' }

/** The rules a manifest edit must pass to be saved: each pinned version is one of its dataset's published versions. */
export function manifestGate(db: Db, manifest: ManifestEntry[]): ReleaseViolation[] {
  const violations: ReleaseViolation[] = []
  const ds = (id: string) => db.datasets.find((d) => d.id === id)
  for (const row of manifest) {
    const v = datasetVersionOf(db, row.datasetVersionId)
    if (!v || v.datasetId !== row.datasetId)
      violations.push({
        code: 'pinned_version_belongs_to_dataset',
        message: `Dataset '${ds(row.datasetId)?.key ?? row.datasetId}' pins dataset version ${row.datasetVersionId}, which is not one of its versions.`,
        datasetId: row.datasetId,
        target: { area: 'dataset', itemId: row.datasetId },
      })
  }
  for (const row of manifest) {
    const v = datasetVersionOf(db, row.datasetVersionId)
    if (v && v.datasetId === row.datasetId && v.status !== 'published')
      violations.push({
        code: 'pinned_version_published',
        message: `Dataset '${ds(row.datasetId)?.key}' pins v${v.versionNumber}, which is ${v.status}; a release pins only published versions.`,
        datasetId: row.datasetId,
        target: { area: 'dataset', itemId: row.datasetId },
      })
  }
  return violations
}

/** The release rules, in the order the API runs them. */
export function runReleaseChecks(db: Db, releaseId: string, unsaved: boolean): ReleaseCheckReport {
  const manifest = manifestOf(db, releaseId)
  const violations: ReleaseViolation[] = []
  const ds = (id: string) => db.datasets.find((d) => d.id === id)
  const ident = (id: string) => db.identities.find((i) => i.id === id)

  if (!manifest.length) violations.push({ code: 'manifest_not_empty', message: 'The release pins no dataset versions.', datasetId: null })
  violations.push(...manifestGate(db, manifest))

  const bindingRule = (code: ReleaseRuleCode, kind: PinKind) => {
    for (const row of manifest) {
      const v = datasetVersionOf(db, row.datasetVersionId)
      if (!v || v.datasetId !== row.datasetId) continue
      for (const bnd of v.bindings) {
        const id = kind === 'dimension' ? bnd.dimensionId : kind === 'mapping_set' ? bnd.mappingSetId : bnd.bridgeId
        if (!id) continue
        const i = ident(id)
        if (i && publishedVersionOf(i)) continue
        const dsKey = ds(row.datasetId)?.key
        const name = i ? (kind === 'dimension' ? i.displayName : i.key) : id
        violations.push({
          code,
          message:
            kind === 'dimension'
              ? `Binding '${bnd.displayName}' of dataset '${dsKey}' binds dimension '${name}', which has no published version.`
              : `Binding '${bnd.displayName}' of dataset '${dsKey}' maps through ${KIND_WORD[kind]} '${name}', which has no published version.`,
          datasetId: row.datasetId,
          target: { area: kind, itemId: id },
        })
      }
    }
  }
  bindingRule('bound_dimensions_resolvable', 'dimension')
  bindingRule('mapped_bindings_resolvable', 'mapping_set')
  bindingRule('bridge_bindings_resolvable', 'bridge')

  const pinnedVersion = (datasetId: string) => {
    const row = manifest.find((r) => r.datasetId === datasetId)
    return row ? datasetVersionOf(db, row.datasetVersionId) : undefined
  }
  const enabledReports = db.reports.filter((x) => x.isEnabled)
  for (const r of enabledReports) {
    for (const s of r.sources) {
      const dsKey = ds(s.datasetId)?.key ?? s.datasetId
      const target = { area: 'report', itemId: r.id, itemKey: s.key } as const
      const v = pinnedVersion(s.datasetId)
      if (!v) {
        violations.push({ code: 'report_sources_resolve', message: `Report '${r.key}' source '${s.key}' reads dataset '${dsKey}', which the release does not pin.`, datasetId: null, target })
        continue
      }
      const missing = s.measureKeys.filter((k) => !v.measures.some((mm) => mm.key === k))
      if (missing.length)
        violations.push({
          code: 'report_sources_resolve',
          message: `Report '${r.key}' source '${s.key}' reads measure '${missing[0]}', which v${v.versionNumber} of '${dsKey}' does not define.`,
          datasetId: s.datasetId,
          target,
        })
    }
  }
  for (const r of enabledReports) {
    for (const s of r.sources.filter((x) => x.usesFactors)) {
      const v = pinnedVersion(s.datasetId)
      if (!v) continue
      const ignoring = s.measureKeys.find((k) => v.measures.find((mm) => mm.key === k)?.appliesBridgeFactors === false)
      if (ignoring)
        violations.push({
          code: 'report_factors_apply',
          message: `Report '${r.key}' source '${s.key}' adds items into rows with factors other than 1, but it reads '${ignoring}' of '${ds(s.datasetId)?.key}', which ignores bridge factors.`,
          datasetId: s.datasetId,
          target: { area: 'report', itemId: r.id, itemKey: s.key },
        })
    }
  }

  return { releaseId, unsaved, checkedAt: new Date().toISOString(), violations }
}

export function readersOf(reports: ReleaseReportRef[], datasetId: string) {
  return reports.filter((r) => r.isEnabled && r.sources.some((s) => s.datasetId === datasetId)).map((r) => r.key)
}

/** What changes going from one release's manifest to another's. */
export function diffReleases(db: Db, fromReleaseId: string, toReleaseId: string): ReleaseDiff {
  const fromMap = new Map(manifestOf(db, fromReleaseId).map((r) => [r.datasetId, r.datasetVersionId]))
  const toMap = new Map(manifestOf(db, toReleaseId).map((r) => [r.datasetId, r.datasetVersionId]))
  const diff: ReleaseDiff = { fromReleaseId, toReleaseId, added: [], removed: [], changed: [], unchanged: 0 }
  for (const [datasetId, versionId] of toMap) {
    const prev = fromMap.get(datasetId)
    if (!prev) diff.added.push({ datasetId, versionId })
    else if (prev !== versionId) diff.changed.push({ datasetId, fromVersionId: prev, toVersionId: versionId })
    else diff.unchanged++
  }
  for (const [datasetId, versionId] of fromMap) {
    if (!toMap.has(datasetId)) diff.removed.push({ datasetId, versionId, readBy: readersOf(db.reports, datasetId) })
  }
  return diff
}

/** The active set's pins: every identity's published version, including report-owned ones. */
export function activePins(db: Db) {
  return db.identities.flatMap((i) => {
    const v = publishedVersionOf(i)
    return v ? [{ kind: i.kind, identityId: i.id, versionId: v.id }] : []
  })
}

export function pinRows(db: Db, setId: string): PinRow[] {
  const set = db.sets.find((s) => s.id === setId)!
  const ident = (id: string) => db.identities.find((i) => i.id === id)!
  const pins = set.status === 'active' ? activePins(db) : db.pins.filter((p) => p.setId === setId)
  return pins.map((p) => {
    const i = ident(p.identityId)
    const status = i.versions.find((v) => v.id === p.versionId)!.status
    return {
      kind: p.kind,
      identityId: p.identityId,
      versionId: p.versionId,
      publishedVersionId: publishedVersionOf(i)?.id ?? null,
      state: set.status === 'active' && i.reportId ? 'report_owned' : status === 'published' ? 'live' : 'frozen',
    }
  })
}

/** Whether a report comparison can name a historical set (A4). */
export function readiness(db: Db, setId: string): SetReadiness[] {
  const set = db.sets.find((s) => s.id === setId)!
  const pinned = new Set(db.pins.filter((p) => p.setId === setId).map((p) => p.identityId))
  const manifest = manifestOf(db, set.releaseId!)
  const release = db.releases.find((r) => r.id === set.releaseId)
  return db.reports
    .filter((r) => r.isEnabled)
    .map((r) => {
      const reasons: SetReadiness['reasons'] = []
      for (const read of r.reads) {
        if (pinned.has(read.identityId)) continue
        const i = db.identities.find((x) => x.id === read.identityId)
        const after = i && set.capturedAt && i.createdAt > set.capturedAt
        reasons.push({
          message: `Does not pin ${KIND_WORD[read.kind]} ${i?.key ?? read.identityId}${after ? ' (created after the capture)' : ''}`,
          target: { area: read.kind, itemId: read.identityId },
        })
      }
      for (const s of r.sources) {
        if (manifest.some((m) => m.datasetId === s.datasetId)) continue
        reasons.push({
          message: `Release ${release?.key} does not pin dataset ${db.datasets.find((d) => d.id === s.datasetId)?.key}, which source '${s.key}' reads.`,
          target: { area: 'report', itemId: r.id, itemKey: s.key },
        })
      }
      return { reportId: r.id, ready: reasons.length === 0, reasons }
    })
}
