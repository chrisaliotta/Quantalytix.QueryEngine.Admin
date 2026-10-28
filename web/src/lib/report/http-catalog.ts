import { refill } from '@/lib/api/changes'
import { unwrap, type Id } from '@/lib/api/contract'
import { tenantHttp } from '@/lib/api/session'
import type { DatasetListItem, DatasetVersion } from '@/lib/datasets/contract'
import { createHttpDatasetsApi } from '@/lib/datasets/http/adapter'
import { tenantApi } from '@/lib/tenant/client'
import type { PublishedMember, TenantCatalog, TenantCatalogDimension, TenantCatalogRole } from '@/lib/tenant/contract'
import { PUBLISHED_DIMENSIONS, ROLES, SCENARIO_ROLES, SOURCE_DIMENSION_ROLES, type DimNode, type Dimension, type RoleInfo } from './dimensions'
import type { Dataset, DimMember } from './types'

/**
 * What the report editor reads from other modules in http mode (OI-55): the roles and their published members (tenant), and
 * the datasets with their measures, fields, time fields and bound roles (datasets). The prototype kept these as copies inside
 * the report and as constants in dimensions.ts; here they are read once, before the app renders, and put in the same places.
 */
export interface ReportCatalog {
  roles: TenantCatalogRole[]
  /** Datasets by id, in the shape the editor reads. */
  datasets: Map<Id, Dataset>
  /** Published members of each role's dimension as an item tree, by role id. */
  items: Map<Id, DimMember[]>
}

function tree<T>(members: PublishedMember[], node: (m: PublishedMember, children: T[]) => T, parentKey: string | null = null): T[] {
  return members.filter((m) => m.parentKey === parentKey).map((m) => node(m, tree(members, node, m.key)))
}

const itemTree = (members: PublishedMember[]) =>
  tree<DimMember>(members, (m, children) => ({ key: m.key, label: m.displayName, ...(children.length ? { children } : {}) }))

function dimensionOf(dim: TenantCatalogDimension, members: PublishedMember[]): Dimension {
  const levels = [...new Set(members.flatMap((m) => (m.levelKey ? [m.levelKey] : [])))]
  return {
    key: dim.key,
    name: dim.displayName,
    version: dim.publishedVersionNumber ?? 0,
    levels: levels.map((key) => ({ key, label: key })),
    members: tree<DimNode>(members, (m, children) => ({
      key: m.key,
      label: m.displayName,
      ...(m.levelKey ? { level: m.levelKey } : {}),
      ...(children.length ? { children } : {}),
    })),
  }
}

/** The version the editor reads a dataset's shape from: the one the active release pins, else the newest published, else the newest. */
const versionOf = (d: DatasetListItem) => d.activeVersionId ?? d.versions.find((v) => v.status === 'published')?.id ?? d.versions[0]?.id

function datasetOf(item: DatasetListItem, version: DatasetVersion, roleKey: Map<Id, string>): Dataset {
  const fieldKey = new Map(version.fields.map((f) => [f.id, f.key]))
  return {
    key: item.key,
    name: item.displayName,
    measures: version.measures.map((m) => m.key),
    fields: version.fields.map((f) => f.key),
    timeFields: version.bindings.flatMap((b) => (b.bindingKind === 'time' && b.sourceFieldId ? [fieldKey.get(b.sourceFieldId)!] : [])),
    roles: version.bindings.flatMap((b) => (b.dimensionRoleId && roleKey.has(b.dimensionRoleId) ? [roleKey.get(b.dimensionRoleId)!] : [])),
  }
}

/** Fills the role lists and published dimensions of dimensions.ts with the tenant's analytical roles. */
function register(catalog: TenantCatalog, roles: TenantCatalogRole[], members: Map<Id, PublishedMember[]>) {
  const dimensions = new Map(catalog.dimensions.map((d) => [d.id, d]))
  const infos: RoleInfo[] = roles.map((r) => ({ role: r.key, name: r.displayName, dimension: dimensions.get(r.dimensionId)!.key, scenario: true }))
  refill(ROLES, infos)
  refill(SCENARIO_ROLES, infos)
  refill(
    SOURCE_DIMENSION_ROLES,
    roles.map((r) => ({ value: r.key, label: r.displayName, hint: r.key })),
  )
  PUBLISHED_DIMENSIONS.clear()
  for (const r of roles) {
    const published = members.get(r.dimensionId)
    if (published) PUBLISHED_DIMENSIONS.set(r.key, dimensionOf(dimensions.get(r.dimensionId)!, published))
  }
}

export async function loadReportCatalog(): Promise<ReportCatalog> {
  const catalog = await tenantApi.getCatalog()
  const analytical = new Set(catalog.dimensions.filter((d) => d.ownerReportId === null && d.publishedVersionId !== null).map((d) => d.id))
  const roles = catalog.roles.filter((r) => analytical.has(r.dimensionId))
  const roleKey = new Map(roles.map((r) => [r.id, r.key]))

  const published = await Promise.all([...analytical].map(async (id) => [id, (await tenantApi.getPublishedMembers(id))?.members ?? []] as const))
  const members = new Map<Id, PublishedMember[]>(published)

  const datasetsApi = createHttpDatasetsApi(tenantHttp)
  const items = (await datasetsApi.listDatasets()).filter((d) => versionOf(d))
  const versions = await Promise.all(items.map((d) => unwrap(datasetsApi.getVersion(versionOf(d)!))))

  register(catalog, roles, members)
  return {
    roles,
    datasets: new Map(items.map((d, i) => [d.id, datasetOf(d, versions[i], roleKey)])),
    items: new Map(roles.map((r) => [r.id, itemTree(members.get(r.dimensionId) ?? [])])),
  }
}
