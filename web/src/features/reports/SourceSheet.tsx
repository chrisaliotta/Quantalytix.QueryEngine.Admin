import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, KeyField, TextField, VocabularyField } from '@/shared/form';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { useCreateReportSource, useUpdateReportSource } from './editorApi';
import { isTimeField } from './timeFields';
import type { EditorSource, EditorView } from './types';

/** Values of the source sheet; a choice not made yet is null. */
interface SourceValues {
  sourceKey: string;
  displayName: string;
  datasetId: number | null;
  mappingMethod: string | null;
  dimensionRoleId: number | null;
  fieldKey: string | null;
  timeFieldKey: string | null;
  copyFromSourceId: number | null;
}

export interface SourceSheetProps {
  view: EditorView;
  /** The source to edit; null to create one. */
  source: EditorSource | null;
  onClose: () => void;
}

/**
 * Creates or edits a report source: a dataset, then how its rows reach the report's rows, by its own bridge from a role the
 * dataset binds, whose items each data row picks, or by a field whose values are row keys; and the time field its rows are read
 * on. A new bridge source can start from a copy of a sibling's items, which later edits of either never share. Dataset, method,
 * role and field are fixed once the source exists. Keys are offered from the dataset's latest version; the active release's
 * version is the one the source is checked against.
 */
export function SourceSheet({ view, source, onClose }: SourceSheetProps) {
  const create = useCreateReportSource(view.report.reportId);
  const update = useUpdateReportSource(view.report.reportId);
  const form = useForm<SourceValues>({
    defaultValues: {
      sourceKey: source?.source.sourceKey ?? '',
      displayName: source?.source.displayName ?? '',
      datasetId: source?.source.datasetId ?? null,
      mappingMethod: source?.source.mappingMethod ?? 'bridge',
      dimensionRoleId: source?.source.dimensionRoleId ?? null,
      fieldKey: source?.source.fieldKey ?? null,
      timeFieldKey: source?.source.timeFieldKey ?? null,
      copyFromSourceId: null,
    },
  });
  const [datasetId, mappingMethod, dimensionRoleId] = useWatch({ control: form.control, name: ['datasetId', 'mappingMethod', 'dimensionRoleId'] });
  const datasetVersions = useLookup('dataset-versions', datasetId);
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const boundRoles = useLatestVersionItems('dataset-versions', datasetId, 'bound-roles');
  const roleDimensionId = boundRoles?.find(role => role.id === dimensionRoleId)?.parentId ?? null;
  const siblings = new Set(
    view.sources
      .filter(sibling => sibling.source.mappingMethod === 'bridge' && sibling.itemDimensionId === roleDimensionId)
      .map(sibling => sibling.source.reportSourceId)
  );
  const isNew = source === null;

  function submit(values: SourceValues) {
    if (!isNew) {
      return update.mutateAsync({
        sourceId: source.source.reportSourceId,
        body: { displayName: values.displayName, timeFieldKey: values.timeFieldKey ?? '' },
      });
    }
    const isBridge = values.mappingMethod === 'bridge';
    return create.mutateAsync({
      sourceKey: values.sourceKey,
      displayName: values.displayName,
      datasetId: values.datasetId ?? 0,
      mappingMethod: values.mappingMethod ?? '',
      dimensionRoleId: isBridge ? values.dimensionRoleId : null,
      fieldKey: isBridge ? null : values.fieldKey,
      timeFieldKey: values.timeFieldKey ?? '',
      copyFromSourceId: isBridge ? values.copyFromSourceId : null,
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={isNew ? 'New source' : `Edit ${source.source.sourceKey}`}
      description="A source reads one dataset; its rows reach the report's rows through a bridge of its own or by a field of row keys."
      form={form}
      onSubmit={submit}
      width="36rem"
    >
      {isNew && (
        <TextField
          name="sourceKey"
          label="Key"
          description="A letter, then letters, digits or underscores; a bridge source's bridge takes it in its key."
        />
      )}
      <TextField name="displayName" label="Name" />
      <EntityField
        name="datasetId"
        label="Dataset"
        lookupKind="datasets"
        disabled={!isNew}
        description="The active release must pin it; reports run against whichever release is active."
      />
      <VocabularyField
        name="mappingMethod"
        label="Reaches the rows"
        vocabulary="report_mapping_method"
        disabled={!isNew}
        description="By bridge: rows classify into a dimension the dataset binds, whose members each data row picks. Direct: a field holds row keys."
      />
      {mappingMethod === 'bridge' ? (
        <EntityField
          name="dimensionRoleId"
          label="Bridge from role"
          lookupKind="bound-roles"
          parentId={datasetVersions.data?.at(-1)?.id ?? null}
          disabled={!isNew}
          description="A role the dataset binds into members; its dimension's members are the items the rows pick."
        />
      ) : (
        <KeyField
          name="fieldKey"
          label="Row key field"
          items={fields}
          disabled={!isNew}
          description="An enabled field whose values are the keys of the report's rows."
        />
      )}
      <KeyField
        name="timeFieldKey"
        label="Time field"
        items={fields && fields.filter(isTimeField)}
        description="A field with a time binding; it places each row in a report period."
      />
      {isNew && mappingMethod === 'bridge' && (
        <EntityField
          name="copyFromSourceId"
          label="Start from the items of"
          lookupKind="report-sources"
          parentId={view.report.reportId}
          filter={item => siblings.has(item.id)}
          allowEmpty
          description="Optional: a bridge source of this report from the same dimension, whose items the new source copies once."
        />
      )}
    </EntityFormSheet>
  );
}
