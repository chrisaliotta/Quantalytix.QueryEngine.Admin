/**
 * Session contract (blueprint 10). Mirrors the `Session` and `AccessibleTenant` schemas of
 * api/openapi/contract/tenant.yaml. There are no tenant roles (OI-09): `isSystemAdmin` only gates the tenant directory.
 */

/** Who the caller is, as the API sees its bearer token. */
export interface Session {
  subject: string
  displayName: string | null
  /** The token's tenant when it carries exactly one tenant claim. */
  tenantId: string | null
  isSystemAdmin: boolean
}

export interface AccessibleTenant {
  tenantId: string
  /** Whether query routing is permitted for the tenant. */
  isEnabled: boolean
}

export interface SessionApi {
  /** `GET /api/me` */
  getSession(): Promise<Session>
  /** `GET /api/tenants`: every directory tenant for a system admin, the token's tenant otherwise. */
  listTenants(): Promise<AccessibleTenant[]>
}
