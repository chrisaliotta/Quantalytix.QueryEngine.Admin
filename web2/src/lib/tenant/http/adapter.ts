import { ProblemError, unwrap } from '@/lib/api/contract'
import type { HttpClient } from '@/lib/api/http'
import type { components } from '@/lib/api/schema'
import type { Id, PublishedMembers, TenantApi, TenantCatalog, TenantOverview, Vocabularies } from '../contract'

type Schemas = components['schemas']

/** The contract keys `componentRolesByKind` by every measure kind (`propertyNames`), which the generated type drops. */
const asRolesByKind = (byKind: Schemas['Vocabularies']['componentRolesByKind']) => byKind as Vocabularies['componentRolesByKind']

/** `TenantApi` over the Admin API; `http` resolves paths under `/api`, tenant reads go under `/api/tenants/{tenantId}`. */
export function createHttpTenantApi(http: HttpClient, tenantId: string): TenantApi {
  const tenant = http.forTenant(tenantId)
  return {
    async getOverview(): Promise<TenantOverview> {
      return unwrap(tenant.get<Schemas['TenantOverview']>('/overview'))
    },
    async getCatalog(): Promise<TenantCatalog> {
      return unwrap(tenant.get<Schemas['TenantCatalog']>('/catalog'))
    },
    async getPublishedMembers(dimensionId: Id): Promise<PublishedMembers | null> {
      const result = await tenant.get<Schemas['PublishedMembers']>(`/dimensions/${dimensionId}/published-members`)
      if (result.ok) return result.data
      if (result.problem.status === 404) return null
      throw new ProblemError(result.problem)
    },
    async getVocabularies(): Promise<Vocabularies> {
      const vocabularies = await unwrap(http.get<Schemas['Vocabularies']>('/vocabularies'))
      return { ...vocabularies, componentRolesByKind: asRolesByKind(vocabularies.componentRolesByKind) }
    },
  }
}
