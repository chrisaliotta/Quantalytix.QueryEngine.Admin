import type { UseFormReturn } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { NumberField, PasswordField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { TestConnectionButton } from './TestConnectionButton';
import type { TenantCreateInput } from './types';
import { useCreateTenant } from './useSystemTenants';

export interface NewTenantSheetProps {
  form: UseFormReturn<TenantCreateInput>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Sheet that registers a tenant; saving verifies that the connection string reaches the tenant's own database. */
export function NewTenantSheet({ form, open, onOpenChange }: NewTenantSheetProps) {
  const create = useCreateTenant();
  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="New tenant"
      description="Routes a tenant to its metadata database. The database's tenant_key must equal the tenant ID."
      form={form}
      onSubmit={create.mutateAsync}
      submitLabel="Register tenant"
    >
      <TextField name="tenantId" label="Tenant ID" description="The trusted tenant identity; it cannot change later." />
      <VocabularyField name="providerId" label="Provider" vocabulary="provider_id" />
      <PasswordField
        name="connectionString"
        label="Connection string"
        description="The complete route, including the tenant database. It is stored as written and never shown again."
      />
      <TestConnectionButton />
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries are routed to the tenant." />
      <NumberField name="metadataCacheSeconds" label="Metadata cache seconds" description="How long the engine may cache metadata." />
    </EntityFormSheet>
  );
}
