import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import type { Dataset } from './types';

interface DatasetEditValues {
  displayName: string;
  isEnabled: boolean;
}

/** Sheet that edits a dataset's display name and availability; mounted only while open. */
export function DatasetEditSheet({ dataset, onClose }: { dataset: Dataset; onClose: () => void }) {
  const tenantId = useTenantId();
  const form = useForm<DatasetEditValues>({
    defaultValues: { displayName: dataset.displayName, isEnabled: dataset.isEnabled },
  });
  const save = useMutation({
    mutationFn: async (body: DatasetEditValues) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/datasets/{datasetId}', {
          params: { path: { tenantId, datasetId: dataset.datasetId } },
          body,
        })
      ),
    onSuccess: () => invalidateTenantQueries(tenantId),
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={`Edit ${dataset.datasetKey}`}
      description="The key is fixed. Saving advances the semantic revision, so the query engine reloads the dataset's identity."
      form={form}
      onSubmit={save.mutateAsync}
    >
      <TextField name="displayName" label="Display name" />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled datasets are not offered to queries." />
    </EntityFormSheet>
  );
}
