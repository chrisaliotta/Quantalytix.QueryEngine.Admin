import type { DatasetSourceObject, Id } from './contract'

/** The source object one dataset version reads. */
export interface VersionSource {
  datasetId: Id
  sourceObject: string
}

/**
 * The managed tables deleting a dataset drops (OI-33): tables the API created (`isManaged`) that the dataset's versions read and no
 * version of another dataset reads. The API drops them, with their materialized views, as part of the delete.
 */
export function managedTablesDroppedWith(datasetId: Id, versions: VersionSource[], objects: DatasetSourceObject[]): string[] {
  const read = new Set(versions.filter((v) => v.datasetId === datasetId).map((v) => v.sourceObject))
  const readElsewhere = new Set(versions.filter((v) => v.datasetId !== datasetId).map((v) => v.sourceObject))
  return objects.filter((o) => o.kind === 'table' && o.isManaged && read.has(o.name) && !readElsewhere.has(o.name)).map((o) => o.name)
}
