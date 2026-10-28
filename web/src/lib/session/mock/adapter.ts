import type { AccessibleTenant, Session, SessionApi } from '../contract'

const session: Session = { subject: 'dana.whitfield', displayName: 'Dana Whitfield', tenantId: 'demo_bank', isSystemAdmin: true }

const tenants: AccessibleTenant[] = [
  { tenantId: 'demo_bank', isEnabled: true },
  { tenantId: 'sandbox', isEnabled: false },
]

export function createMockSessionApi(): SessionApi {
  return {
    async getSession() {
      return structuredClone(session)
    },
    async listTenants() {
      return structuredClone(tenants)
    },
  }
}
