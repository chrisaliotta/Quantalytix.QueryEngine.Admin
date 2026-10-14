import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { useTenantId } from '@/shared/useTenantId';
import { ColumnsField } from './ColumnsField';
import { unusedColumns } from './fieldForm';
import { FormDialog } from './FormDialog';
import { useSourceColumns } from './queries';
import type { DatasetVersion, Field } from './types';

interface AddFromColumnsValues {
  columns: string[];
}

export interface AddFromColumnsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  version: DatasetVersion;
  /** The version's fields; columns that a column field already reads are not offered. */
  fields: Field[];
}

/** Dialog that creates a column field, named and typed from the column, for each selected unused column. */
export function AddFromColumnsDialog({ open, onOpenChange, version, fields }: AddFromColumnsDialogProps) {
  const tenantId = useTenantId();
  const columns = useSourceColumns(version.sourceObject);
  const available = unusedColumns(columns.data ?? [], fields);
  const form = useForm<AddFromColumnsValues>({ defaultValues: { columns: [] } });
  const create = useMutation({
    mutationFn: async (body: AddFromColumnsValues) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields/from-columns', {
          params: { path: { tenantId, versionId: version.datasetVersionId } },
          body,
        })
      ),
    onSuccess: created => {
      toast.success(`Added ${created.length} field${created.length === 1 ? '' : 's'}.`);
      form.reset({ columns: [] });
      return invalidateTenantQueries(tenantId);
    },
  });

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Add fields from columns"
      description={`Creates a column field for each selected column of ${version.sourceObject}: the key in camelCase, the name in Title Case, the type from the column.`}
      form={form}
      onSubmit={create.mutateAsync}
      submitLabel="Add fields"
    >
      <ColumnsField name="columns" label="Columns" columns={available} />
      {columns.isSuccess && available.length === 0 && (
        <p className="text-sm text-muted-foreground">Every column with a field type already has a column field.</p>
      )}
    </FormDialog>
  );
}
