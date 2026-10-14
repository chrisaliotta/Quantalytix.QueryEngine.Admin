import { useMutation } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { ExpressionField, NumberField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { fieldFormValues, readsColumn, toFieldInput, toFieldUpdateInput, type FieldFormValues } from './fieldForm';
import { SourceColumnField } from './SourceColumnField';
import type { DatasetVersion, Field, SourceColumn } from './types';

export interface FieldSheetProps {
  version: DatasetVersion;
  /** The field to edit; absent for a new field. */
  field?: Field;
  /** The version's field keys, completed after `[f.` in expressions. */
  fieldKeys: string[];
  /** Whether the version is a draft; the sheet is read-only otherwise. */
  isDraft: boolean;
  onClose: () => void;
}

/**
 * Sheet that creates or edits a field, showing the source inputs of the chosen kind: a source column for column
 * fields, a JSON column and path for json_path fields, an expression over the version's fields for expression fields.
 * Mounted only while open.
 */
export function FieldSheet({ version, field, fieldKeys, isDraft, onClose }: FieldSheetProps) {
  const tenantId = useTenantId();
  const versionId = version.datasetVersionId;
  const form = useForm<FieldFormValues>({ defaultValues: fieldFormValues(field) });
  const [kind, dataType] = useWatch({ control: form.control, name: ['fieldSourceKind', 'dataType'] });
  const save = useMutation({
    mutationFn: async (values: FieldFormValues) => {
      if (field) {
        return unwrap(
          await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}', {
            params: { path: { tenantId, versionId, fieldId: field.fieldId } },
            body: toFieldUpdateInput(values),
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/fields', {
          params: { path: { tenantId, versionId } },
          body: toFieldInput(values),
        })
      );
    },
    onSuccess: () => invalidateTenantQueries(tenantId),
  });

  function takeColumnType(column: SourceColumn) {
    if (kind === 'column' && column.dataType) {
      form.setValue('dataType', column.dataType);
      form.setValue('numericPrecision', column.numericPrecision ?? null);
      form.setValue('numericScale', column.numericScale ?? null);
    }
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={field?.fieldKey ?? 'New field'}
      description={`A logical field of ${version.sourceObject}, version ${version.versionNumber}.`}
      form={form}
      onSubmit={save.mutateAsync}
      readOnly={!isDraft}
      width="36rem"
    >
      <TextField
        name="fieldKey"
        label="Key"
        description="The request-facing field ID; fixed once created."
        disabled={field !== undefined}
      />
      <TextField name="displayName" label="Display name" />
      <VocabularyField name="fieldSourceKind" label="Source kind" vocabulary="field_source_kind" />
      {readsColumn(kind) && (
        <SourceColumnField
          name="sourceColumn"
          label={kind === 'json_path' ? 'JSON column' : 'Source column'}
          sourceObject={version.sourceObject}
          onColumnSelected={takeColumnType}
        />
      )}
      {kind === 'json_path' && (
        <TextField
          name="jsonPath"
          label="JSON path"
          description="Such as $.address.state; a value that does not convert to the data type reads as null."
        />
      )}
      {kind === 'expression' && (
        <ExpressionField
          name="expressionText"
          label="Expression"
          description="A row expression over other fields, such as [f.balance] * [f.rate]."
          fieldKeys={fieldKeys}
        />
      )}
      <VocabularyField name="dataType" label="Data type" vocabulary="data_type" />
      {dataType === 'decimal' && (
        <div className="grid grid-cols-2 gap-4">
          <NumberField name="numericPrecision" label="Precision" />
          <NumberField name="numericScale" label="Scale" />
        </div>
      )}
      <TextField name="unitKey" label="Unit" description="Optional semantic unit, such as USD, percent or days." />
      <SwitchField name="isNullable" label="Nullable" description="The field may be null." />
      <SwitchField name="isSelectable" label="Selectable" description="May appear in detail queries." />
      <SwitchField name="isFilterable" label="Filterable" description="May be referenced in row filters." />
      <SwitchField name="isSortable" label="Sortable" description="May order query results." />
      <SwitchField name="isGroupable" label="Groupable" description="May group aggregate queries directly." />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled fields are not offered to queries." />
    </EntityFormSheet>
  );
}
