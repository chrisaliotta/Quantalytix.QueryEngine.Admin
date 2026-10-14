import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { TextField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { useTenantId } from '@/shared/useTenantId';
import type { DatasetVersion, Field } from './types';

interface MaterializeValues {
  columnName: string;
}

export interface MaterializeDialogProps {
  /** The json_path field to materialize. */
  field: Field;
  /** Its draft version, whose source object is a table. */
  version: DatasetVersion;
  onClose: () => void;
}

/**
 * Dialog that adds a generated column holding a json_path field's value to the source table and points the field at
 * it; it shows progress while StarRocks applies the schema change. Mounted only while open.
 */
export function MaterializeDialog({ field, version, onClose }: MaterializeDialogProps) {
  const tenantId = useTenantId();
  const form = useForm<MaterializeValues>({ defaultValues: { columnName: snakeCase(field.fieldKey) } });
  const materialize = useMutation({
    mutationFn: async (body: MaterializeValues) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}/materialize', {
          params: { path: { tenantId, versionId: version.datasetVersionId, fieldId: field.fieldId } },
          body,
        })
      ),
    onSuccess: (_, body) => {
      toast.success(`${field.fieldKey} now reads the generated column ${body.columnName}.`);
      return invalidateTenantQueries(tenantId);
    },
  });

  return (
    <FormDialog
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={`Materialize ${field.fieldKey}`}
      description={`Adds a generated column to ${version.sourceObject} holding ${field.jsonPath} of ${field.sourceColumn} as ${field.dataType}, then points this draft field at it. Every version that reads the table shares the column; published versions keep reading the JSON.`}
      form={form}
      onSubmit={materialize.mutateAsync}
      submitLabel="Materialize"
      pendingMessage="StarRocks is applying the schema change; this can take a minute."
    >
      <TextField name="columnName" label="Column name" description="A new column of the table." />
    </FormDialog>
  );
}

/** The snake_case column name suggested for a camelCase field key: regionCode becomes region_code. */
function snakeCase(fieldKey: string) {
  return fieldKey.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toLowerCase();
}
