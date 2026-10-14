import { useMutation } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { SourceObjectField } from './SourceObjectField';
import type { Dataset } from './types';

interface NewDatasetValues {
  datasetKey: string;
  displayName: string;
  isEnabled: boolean;
  sourceObjectKind: string | null;
  sourceObject: string | null;
}

export interface NewDatasetSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Called with the new dataset, e.g. to open its page. */
  onCreated: (dataset: Dataset) => void;
}

/** Sheet that creates a dataset with an empty draft version 1 over a table or view of the tenant database. */
export function NewDatasetSheet({ open, onOpenChange, onCreated }: NewDatasetSheetProps) {
  const tenantId = useTenantId();
  const form = useForm<NewDatasetValues>({
    defaultValues: { datasetKey: '', displayName: '', isEnabled: true, sourceObjectKind: 'table', sourceObject: null },
  });
  const kind = useWatch({ control: form.control, name: 'sourceObjectKind' });
  const create = useMutation({
    mutationFn: async (values: NewDatasetValues) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/datasets', {
          params: { path: { tenantId } },
          body: {
            datasetKey: values.datasetKey,
            displayName: values.displayName,
            isEnabled: values.isEnabled,
            sourceObjectKind: values.sourceObjectKind ?? '',
            sourceObject: values.sourceObject ?? '',
          },
        })
      ),
    onSuccess: dataset => {
      onCreated(dataset);
      return invalidateTenantQueries(tenantId);
    },
  });

  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="New dataset"
      description="Creates the dataset with an empty draft version 1 over a table or view."
      form={form}
      onSubmit={create.mutateAsync}
      submitLabel="Create"
    >
      <TextField
        name="datasetKey"
        label="Key"
        description="The ID query requests name, such as mortgage.loan_monthly; fixed once created."
      />
      <TextField name="displayName" label="Display name" />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled datasets are not offered to queries." />
      <VocabularyField name="sourceObjectKind" label="Source kind" vocabulary="source_object_kind" />
      <SourceObjectField name="sourceObject" label="Source object" kind={kind} />
    </EntityFormSheet>
  );
}
