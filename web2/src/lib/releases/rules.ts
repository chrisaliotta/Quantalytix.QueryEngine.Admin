import type {
  CheckReport,
  Identity,
  ManifestEntry,
  PinRow,
  Readiness,
  ReleaseDiff,
  ReleaseViolation,
  ReportRef,
} from './contract'
import type { Db } from './mock/data'

export const publishedVersionOf = (i: Identity) => i.versions.find((v) => v.status === 'published') ?? null

export function manifestOf(db: Db, releaseId: string): ManifestEntry[] {
  return db.manifests.filter((m) => m.releaseId === releaseId).map(({ datasetId, datasetVersionId }) => ({ datasetId, datasetVersionId }))
}

const KIND_WORD = { dimension: 'dimension', mapping_set: 'mapping set', bridge: 'bridge' } as const

/** The release rules of A2, in the order the API runs them. */
export function runReleaseChecks(db: Db, releaseId: string, manifest: ManifestEntry[], unsaved: boolean): CheckReport {
  const violations: ReleaseViolation[] = []
  const ds = (id: string) => db.datasets.find((d) => d.id === id)
  const ver = (id: string) => db.versions.find((v) => v.id === id)
  const ident = (id: string) => db.identities.find((i) => i.id === id)

  if (!manifest.length) violations.push({ rule: 'manifest_not_empty', message: 'The release pins no dataset versions.', datasetId: null, target: null })

  for (const row of manifest) {
    const d = ds(row.datasetId)
    const v = ver(row.datasetVersionId)
    if (!v || v.datasetId !== row.datasetId) {
      violations.push({
        rule: 'pinned_version_belongs_to_dataset',
        message: `Dataset '${d?.key ?? row.datasetId}' pins dataset version ${row.datasetVersionId}, which is not one of its versions.`,
        datasetId: row.datasetId,
        target: { type: 'dataset', datasetId: row.datasetId },
      })
    }
  }
  for (const row of manifest) {
    const v = ver(row.datasetVersionId)
    if (v && v.datasetId === row.datasetId && v.status !== 'published') {
      violations.push({
        rule: 'pinned_version_published',
        message: `Dataset '${ds(row.datasetId)?.key}' pins v${v.versionNumber}, which is ${v.status}; a release pins only published versions.`,
        datasetId: row.datasetId,
        target: { type: 'dataset', datasetId: row.datasetId },
      })
    }
  }

  const bindingRule = (rule: ReleaseViolation['rule'], kind: 'dimension' | 'mapping_set' | 'bridge') => {
    for (const row of manifest) {
      const v = ver(row.datasetVersionId)
      if (!v || v.datasetId !== row.datasetId) continue
      for (const bnd of v.bindings) {
        const id = kind === 'dimension' ? bnd.dimensionId : kind === 'mapping_set' ? bnd.mappingSetId : bnd.bridgeId
        if (!id) continue
        const i = ident(id)
        if (i && publishedVersionOf(i)) continue
        const dsKey = ds(row.datasetId)?.key
        const name = i ? (kind === 'dimension' ? i.displayName : i.key) : id
        violations.push({
          rule,
          message:
            kind === 'dimension'
              ? `Binding '${bnd.name}' of dataset '${dsKey}' binds dimension '${name}', which has no published version.`
              : `Binding '${bnd.name}' of dataset '${dsKey}' maps through ${KIND_WORD[kind]} '${name}', which has no published version.`,
          datasetId: row.datasetId,
          target: { type: 'identity', kind, identityId: id },
        })
      }
    }
  }
  bindingRule('bound_dimensions_resolvable', 'dimension')
  bindingRule('mapped_bindings_resolvable', 'mapping_set')
  bindingRule('bridge_bindings_resolvable', 'bridge')

  const pinnedVersion = (datasetId: string) => {
    const row = manifest.find((r) => r.datasetId === datasetId)
    return row ? ver(row.datasetVersionId) : undefined
  }
  for (const r of db.reports.filter((x) => x.isEnabled)) {
    for (const s of r.sources) {
      const dsKey = ds(s.datasetId)?.key ?? s.datasetId
      const v = pinnedVersion(s.datasetId)
      if (!v) {
        violations.push({
          rule: 'report_sources_resolve',
          message: `Report '${r.key}' source '${s.key}' reads dataset '${dsKey}', which the release does not pin.`,
          datasetId: null,
          target: { type: 'report', reportId: r.id, sourceKey: s.key },
        })
        continue
      }
      const missing = s.measureKeys.filter((k) => !v.measures.some((mm) => mm.key === k))
      if (missing.length) {
        violations.push({
          rule: 'report_sources_resolve',
          message: `Report '${r.key}' source '${s.key}' reads measure '${missing[0]}', which v${v.versionNumber} of '${dsKey}' does not define.`,
          datasetId: s.datasetId,
          target: { type: 'report', reportId: r.id, sourceKey: s.key },
        })
      }
    }
  }
  for (const r of db.reports.filter((x) => x.isEnabled)) {
    for (const s of r.sources.filter((x) => x.usesFactors)) {
      const v = pinnedVersion(s.datasetId)
      if (!v) continue
      const ignoring = s.measureKeys.find((k) => v.measures.find((mm) => mm.key === k)?.appliesBridgeFactors === false)
      if (ignoring) {
        violations.push({
          rule: 'report_factors_apply',
          message: `Report '${r.key}' source '${s.key}' adds items into rows with factors other than 1, but variable '${s.variable ?? s.key.toUpperCase()}' reads '${ignoring}' of '${ds(s.datasetId)?.key}', which ignores bridge factors.`,
          datasetId: s.datasetId,
          target: { type: 'report', reportId: r.id, sourceKey: s.key },
        })
      }
    }
  }

  return { releaseId, unsaved, violations, ranAt: new Date().toISOString() }
}

export function readersOf(reports: ReportRef[], datasetId: string) {
  return reports.filter((r) => r.isEnabled && r.sources.some((s) => s.datasetId === datasetId)).map((r) => r.key)
}

/** What changes going from one manifest to another. */
export function diffManifests(db: Db, fromReleaseId: string, from: ManifestEntry[], toReleaseId: string, to: ManifestEntry[]): ReleaseDiff {
  const fromMap = new Map(from.map((r) => [r.datasetId, r.datasetVersionId]))
  const toMap = new Map(to.map((r) => [r.datasetId, r.datasetVersionId]))
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
  const set = db.sets.find((s) => s.id === setId)
  if (!set) return []
  const ident = (id: string) => db.identities.find((i) => i.id === id) as Identity
  const pins = set.status === 'active' ? activePins(db) : db.pins.filter((p) => p.setId === setId)
  return pins.map((p) => {
    const i = ident(p.identityId)
    const status = i.versions.find((v) => v.id === p.versionId)?.status
    const published = publishedVersionOf(i)
    return {
      kind: p.kind,
      identityId: p.identityId,
      versionId: p.versionId,
      publishedVersionId: published?.id ?? null,
      state: set.status === 'active' && i.reportId ? 'report_owned' : status === 'published' ? 'live' : status === 'retired' ? 'frozen' : 'unpublished',
    }
  })
}

/** Whether a report comparison can name a historical set (A4). */
export function readiness(db: Db, setId: string): Readiness[] {
  const set = db.sets.find((s) => s.id === setId)
  if (!set || set.status !== 'historical' || !set.releaseId) return []
  const pinned = new Set(db.pins.filter((p) => p.setId === setId).map((p) => p.identityId))
  const manifest = manifestOf(db, set.releaseId)
  const release = db.releases.find((r) => r.id === set.releaseId)
  return db.reports
    .filter((r) => r.isEnabled)
    .map((r) => {
      const reasons: Readiness['reasons'] = []
      for (const read of r.reads) {
        if (pinned.has(read.identityId)) continue
        const i = db.identities.find((x) => x.id === read.identityId)
        const after = i && set.capturedAt && i.createdAt > set.capturedAt
        reasons.push({
          message: `Does not pin ${KIND_WORD[read.kind]} ${i?.key ?? read.identityId}${after ? ' (created after the capture)' : ''}`,
          target: { type: 'identity', kind: read.kind, identityId: read.identityId },
        })
      }
      for (const s of r.sources) {
        if (manifest.some((m) => m.datasetId === s.datasetId)) continue
        reasons.push({
          message: `Release ${release?.key} does not pin dataset ${db.datasets.find((d) => d.id === s.datasetId)?.key}, which source '${s.key}' reads.`,
          target: { type: 'report', reportId: r.id, sourceKey: s.key },
        })
      }
      return { reportId: r.id, ready: reasons.length === 0, reasons }
    })
}
