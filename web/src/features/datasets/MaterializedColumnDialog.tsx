import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { FormDialog } from '@/shared/FormDialog';
import { useTenantId } from '@/shared/useTenantId';
import { SourceColumnField } from './SourceColumnField';
import type { DatasetVersion, Field } from './types';

interface MaterializedColumnValues {
  columnName: string | null;
}

export interface MaterializedColumnDialogProps {
  /** The json_path or expression field. */
  field: Field;
  /** Its draft version. */
  version: DatasetVersion;
  onClose: () => void;
}

/** Dialog that points a json_path or expression field at an existing column of the source object, or clears it. */
export function MaterializedColumnDialog({ field, version, onClose }: MaterializedColumnDialogProps) {
  const tenantId = useTenantId();
  const form = useForm<MaterializedColumnValues>({ defaultValues: { columnName: field.materializedColumn ?? null } });
  const save = useMutation({
    mutationFn: async (body: MaterializedColumnValues) =>
      unwrap(
        await apiClient.PUT(
          '/api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}/materialized-column',
          { params: { path: { tenantId, versionId: version.datasetVersionId, fieldId: field.fieldId } }, body }
        )
      ),
    onSuccess: () => invalidateTenantQueries(tenantId),
  });

  return (
    <FormDialog
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={`Materialized column of ${field.fieldKey}`}
      description="Queries read this column instead of computing the field, so it must hold exactly the field's value. Clear it to compute the value again."
      form={form}
      onSubmit={save.mutateAsync}
      submitLabel="Save"
    >
      <SourceColumnField name="columnName" label="Column" sourceObject={version.sourceObject} allowEmpty />
    </FormDialog>
  );
}
