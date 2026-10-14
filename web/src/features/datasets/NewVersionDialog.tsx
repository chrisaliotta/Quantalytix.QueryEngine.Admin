import { useMutation } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { VocabularyField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useTenantId } from '@/shared/useTenantId';
import { SourceObjectField } from './SourceObjectField';
import type { DatasetVersion } from './types';

interface NewVersionValues {
  sourceObjectKind: string | null;
  sourceObject: string | null;
}

export interface NewVersionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  datasetId: number;
  /** Called with the new draft, e.g. to select it. */
  onCreated: (version: DatasetVersion) => void;
}

/** Dialog that creates an empty draft version of a dataset over a table or view. */
export function NewVersionDialog({ open, onOpenChange, datasetId, onCreated }: NewVersionDialogProps) {
  const tenantId = useTenantId();
  const form = useForm<NewVersionValues>({ defaultValues: { sourceObjectKind: 'table', sourceObject: null } });
  const kind = useWatch({ control: form.control, name: 'sourceObjectKind' });
  const create = useMutation({
    mutationFn: async (values: NewVersionValues) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/datasets/{datasetId}/versions', {
          params: { path: { tenantId, datasetId } },
          body: { sourceObjectKind: values.sourceObjectKind ?? '', sourceObject: values.sourceObject ?? '' },
        })
      ),
    onSuccess: version => {
      toast.success(`Created draft v${version.versionNumber}.`);
      onCreated(version);
      return invalidateTenantQueries(tenantId);
    },
  });

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title="New version"
      description="Creates an empty draft over a table or view. To start from an existing version, select it and use Clone selected."
      form={form}
      onSubmit={create.mutateAsync}
      submitLabel="Create draft"
    >
      <VocabularyField name="sourceObjectKind" label="Source kind" vocabulary="source_object_kind" />
      <SourceObjectField name="sourceObject" label="Source object" kind={kind} />
    </FormDialog>
  );
}
