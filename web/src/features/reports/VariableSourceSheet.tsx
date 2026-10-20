import { useForm, useWatch } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, JsonField, KeyField, SwitchField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLatestVersionItems, usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useSaveVersionContent } from '@/shared/versionContent';
import { ClassificationFields } from './ClassificationFields';
import { classificationValues, toClassificationInput, type ClassificationValues } from './classificationOptions';
import type { Report, ReportVersion, VariableSource } from './types';

/** Data types a time field can have. */
const TIME_TYPES = ['date', 'datetime', 'timestamp'];

/** Values of the source sheet; a choice not made yet is null, and the filter is JSON text, empty for none. */
interface VariableSourceValues {
  variableKey: string | null;
  sourceKey: string;
  datasetId: number | null;
  classification: ClassificationValues;
  timeFieldKey: string | null;
  measureKey: string | null;
  sourceFilterJson: string;
  isEnabled: boolean;
}

export interface VariableSourceSheetProps {
  report: Report;
  version: ReportVersion;
  /** The source to edit or view; null to create one. */
  source: VariableSource | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a variable source: the dataset a variable of the model reads, and how its rows classify into the
 * report's dimension. It names the dataset's keys rather than a version, so the keys offered come from the dataset's latest
 * version; validating a release checks them in the version it pins.
 */
export function VariableSourceSheet({ report, version, source, readOnly, onClose }: VariableSourceSheetProps) {
  const save = useSaveVersionContent('variable-sources', version.reportVersionId, source?.variableSourceId ?? null);
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const form = useForm<VariableSourceValues>({
    defaultValues: {
      variableKey: source?.variableKey ?? null,
      sourceKey: source?.sourceKey ?? '',
      datasetId: source?.datasetId ?? null,
      classification: classificationValues(
        source && {
          kind: source.classificationKind,
          fieldKey: source.classificationFieldKey,
          roleId: source.classificationRoleId,
          mappingSetId: source.mappingSetId,
          bridgeId: source.bridgeId,
        }
      ),
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
      variableKey: values.variableKey ?? '',
      sourceKey: values.sourceKey,
      datasetId: values.datasetId ?? 0,
      classification: toClassificationInput(values.classification),
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
      description="Its rows classify into the report's dimension; the classification is checked against that dimension when it is saved."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
      width="36rem"
    >
      <KeyField
        name="variableKey"
        label="Variable"
        items={variables}
        description="A variable of the model's published version, the one the active assumption set pins."
      />
      <TextField name="sourceKey" label="Key" description="A letter, then letters, digits or underscores." />
      <EntityField name="datasetId" label="Dataset" lookupKind="datasets" />
      <ClassificationFields datasetId={datasetId} targetDimensionId={report.reportDimensionId} />
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
