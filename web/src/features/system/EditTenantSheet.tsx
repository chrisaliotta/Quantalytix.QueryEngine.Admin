import type { UseFormReturn } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { NumberField, TextField, VocabularyField } from '@/shared/form';
import { PasswordField } from './PasswordField';
import { TestConnectionButton } from './TestConnectionButton';
import type { TenantEditValues } from './types';
import { useUpdateTenant } from './useSystemTenants';

export interface EditTenantSheetProps {
  form: UseFormReturn<TenantEditValues>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Sheet that edits a tenant's route; a blank connection string keeps the current one, a typed one is verified. */
export function EditTenantSheet({ form, open, onOpenChange }: EditTenantSheetProps) {
  const update = useUpdateTenant();

  function submit(values: TenantEditValues) {
    return update.mutateAsync({
      tenantId: values.tenantId,
      body: {
        providerId: values.providerId,
        connectionString: values.connectionString || null,
        metadataCacheSeconds: values.metadataCacheSeconds,
      },
    });
  }

  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="Edit tenant"
      description="Enable or disable the tenant from the list."
      form={form}
      onSubmit={submit}
    >
      <TextField name="tenantId" label="Tenant ID" disabled />
      <VocabularyField name="providerId" label="Provider" vocabulary="provider_id" />
      <PasswordField
        name="connectionString"
        label="New connection string"
        description="Leave blank to keep the current connection string. A new one is verified before it is saved."
      />
      <TestConnectionButton />
      <NumberField name="metadataCacheSeconds" label="Metadata cache seconds" description="How long the engine may cache metadata." />
    </EntityFormSheet>
  );
}
