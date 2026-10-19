import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, JsonField, KeyField, SwitchField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useSaveModelContent } from './api';
import { contentSheetTitle } from './contentSheet';
import type { VariableSource } from './types';

/** Data types a time field can have. */
const TIME_TYPES = ['date', 'datetime', 'timestamp'];

/** Values of the source sheet; a choice not made yet is null, and the filter is JSON text, empty for none. */
interface VariableSourceValues {
  variableId: number | null;
  sourceKey: string;
  datasetId: number | null;
  reportFieldKey: string | null;
  timeFieldKey: string | null;
  measureKey: string | null;
  sourceFilterJson: string;
  isEnabled: boolean;
}

export interface VariableSourceSheetProps {
  versionId: number;
  /** The source to edit or view; null to create one. */
  source: VariableSource | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a variable source. It names its dataset and the dataset's keys rather than a version, so the
 * keys offered come from the dataset's latest version; validating a release checks them in the version it pins.
 */
export function VariableSourceSheet({ versionId, source, readOnly, onClose }: VariableSourceSheetProps) {
  const save = useSaveModelContent('variable-sources', versionId, source?.variableSourceId ?? null);
  const form = useForm<VariableSourceValues>({
    defaultValues: {
      variableId: source?.variableId ?? null,
      sourceKey: source?.sourceKey ?? '',
      datasetId: source?.datasetId ?? null,
      reportFieldKey: source?.reportFieldKey ?? null,
      timeFieldKey: source?.timeFieldKey ?? null,
      measureKey: source?.measureKey ?? null,
      sourceFilterJson: source?.sourceFilterJson ?? '',
      isEnabled: source?.isEnabled ?? true,
    },
  });
  const datasetId = useWatch({ control: form.control, name: 'datasetId' });
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const measures = useLatestVersionItems('dataset-versions', datasetId, 'measures');

  function submit(values: VariableSourceValues) {
    return save({
      variableId: values.variableId ?? 0,
      sourceKey: values.sourceKey,
      datasetId: values.datasetId ?? 0,
      reportFieldKey: values.reportFieldKey ?? '',
      timeFieldKey: values.timeFieldKey ?? '',
      measureKey: values.measureKey ?? '',
      sourceFilterJson: emptyToNull(values.sourceFilterJson),
      isEnabled: values.isEnabled,
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('source', source?.sourceKey ?? null, readOnly)}
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
      width="36rem"
    >
      <EntityField name="variableId" label="Variable" lookupKind="variables" parentId={versionId} />
      <TextField name="sourceKey" label="Key" description="A letter, then letters, digits or underscores." />
      <EntityField name="datasetId" label="Dataset" lookupKind="datasets" />
      <KeyField
        name="reportFieldKey"
        label="Report field"
        items={fields}
        description="A field bound to the report dimension by member, pinned to one version, as a release requires."
      />
      <KeyField
        name="timeFieldKey"
        label="Time field"
        items={fields && fields.filter(field => TIME_TYPES.includes(field.classifier ?? ''))}
        description="A field with a time binding; it places each row in a report period."
      />
      <KeyField name="measureKey" label="Measure" items={measures} />
      <JsonField
        name="sourceFilterJson"
        label="Filter"
        description={`Optional: a structured filter over the dataset's fields, such as {"operator":"eq","left":{"type":"field","id":"accountKey"},"right":{"type":"literal","value":"4000"}}.`}
      />
      <SwitchField name="isEnabled" label="Enabled" description="Whether profiles may pick the source." />
    </EntityFormSheet>
  );
}
