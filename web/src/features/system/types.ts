import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A tenant directory row with its connection string's password masked. */
export type TenantDirectoryEntry = Schemas['TenantDirectoryEntry'];

/** The body of `POST /api/system/tenants`. */
export type TenantCreateInput = Schemas['TenantCreateInput'];

/** The body of `PUT /api/system/tenants/{tenantId}`; a null connection string keeps the current one. */
export type TenantUpdateInput = Schemas['TenantUpdateInput'];

/** The outcome of a connection test. */
export type TenantConnectionCheck = Schemas['TenantConnectionCheck'];

/** The edit sheet's values: the fixed tenant ID plus the editable route; a blank connection string keeps the current one. */
export interface TenantEditValues {
  tenantId: string;
  providerId: string;
  connectionString: string;
  metadataCacheSeconds: number;
}
