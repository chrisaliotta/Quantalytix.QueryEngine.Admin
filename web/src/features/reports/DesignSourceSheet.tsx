import { useForm, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, JsonField, KeyField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLatestVersionItems, usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { useCreateDesignSource, useUpdateDesignSource } from './designApi';
import { isTimeField } from './timeFields';
import type { DesignSource, ReportVersion } from './types';

/**
 * Values of the source sheet; a choice not made yet is null and the filter is JSON text. A variable whose measure is empty, or
 * was never picked and so has no value at all, is not supplied.
 */
interface DesignSourceValues {
  key: string;
  label: string;
  datasetId: number | null;
  roleId: number | null;
  timeFieldKey: string | null;
  sourceFilterJson: string;
  copyMappingsFromBridgeId: number | null;
  /** The measure that supplies each model variable, by variable key. */
  measures: Record<string, string | null>;
}

export interface DesignSourceSheetProps {
  version: ReportVersion;
  /** The source to edit or view; null to create one. */
  source: DesignSource | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a design source: a dataset, the role it binds a dimension through, whose members the source maps to
 * the report's rows, the time field and filter its rows are read with, and the measure that supplies each variable of the
 * model's published version. A new source can start with the mappings of a published bridge from the same dimension. Keys are
 * offered from the dataset's latest version; validating a release checks them in the version it pins.
 */
export function DesignSourceSheet({ version, source, readOnly, onClose }: DesignSourceSheetProps) {
  const create = useCreateDesignSource(version.reportVersionId);
  const update = useUpdateDesignSource(version.reportVersionId);
  const variables = usePublishedVersionItems('model-versions', version.modelId, 'variables');
  const form = useForm<DesignSourceValues>({
    defaultValues: {
      key: source?.key ?? '',
      label: source?.label ?? '',
      datasetId: source?.datasetId ?? null,
      roleId: source?.roleId ?? null,
      timeFieldKey: source?.timeFieldKey ?? null,
      sourceFilterJson: source?.sourceFilterJson ?? '',
      copyMappingsFromBridgeId: null,
      measures: Object.fromEntries(source?.variables.map(variable => [variable.variableKey, variable.measureKey]) ?? []),
    },
  });
  const [datasetId, roleId] = useWatch({ control: form.control, name: ['datasetId', 'roleId'] });
  const datasetVersions = useLookup('dataset-versions', datasetId);
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const measures = useLatestVersionItems('dataset-versions', datasetId, 'measures');
  const boundRoles = useLatestVersionItems('dataset-versions', datasetId, 'bound-roles');
  const roleDimensionId = boundRoles?.find(role => role.id === roleId)?.parentId ?? null;

  function submit(values: DesignSourceValues) {
    const body = {
      label: values.label,
      datasetId: values.datasetId ?? 0,
      roleId: values.roleId ?? 0,
      timeFieldKey: values.timeFieldKey ?? '',
      sourceFilterJson: emptyToNull(values.sourceFilterJson),
      variables: Object.entries(values.measures)
        .filter((entry): entry is [string, string] => Boolean(entry[1]))
        .map(([variableKey, measureKey]) => ({ variableKey, measureKey })),
    };
    if (source === null) {
      return create.mutateAsync({ ...body, key: values.key, copyMappingsFromBridgeId: values.copyMappingsFromBridgeId });
    }
    return update.mutateAsync({ bridgeId: source.bridgeId, body });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={source === null ? 'New source' : readOnly ? `Source ${source.key}` : `Edit ${source.key}`}
      description="A source reads one dataset and maps the members of a dimension the dataset binds to the report's rows."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
      width="36rem"
    >
      {source === null && (
        <TextField
          name="key"
          label="Key"
          description="A letter, then letters, digits or underscores; the source's bridge and variable sources take it in their keys."
        />
      )}
      <TextField name="label" label="Label" />
      <EntityField name="datasetId" label="Dataset" lookupKind="datasets" />
      <EntityField
        name="roleId"
        label="Bound role"
        lookupKind="bound-roles"
        parentId={datasetVersions.data?.at(-1)?.id ?? null}
        filter={source === null ? undefined : role => role.parentId === source.sourceDimensionId}
        description={
          source === null
            ? 'A role the dataset binds into members; the source maps the members of its dimension to rows.'
            : "A role of the source's bound dimension, which stays once the source exists."
        }
      />
      <KeyField
        name="timeFieldKey"
        label="Time field"
        items={fields && fields.filter(isTimeField)}
        description="A field with a time binding; it places each row in a report period."
      />
      <JsonField
        name="sourceFilterJson"
        label="Filter"
        description={`Optional: a structured filter over the dataset's fields, such as {"operator":"eq","left":{"type":"field","id":"accountKey"},"right":{"type":"literal","value":"4000"}}.`}
      />
      {source === null && (
        <EntityField
          name="copyMappingsFromBridgeId"
          label="Start mappings from"
          lookupKind="bridges"
          parentId={roleDimensionId}
          allowEmpty
          description="Optional: a bridge from the role's dimension whose published mappings into this report's rows the source starts with."
        />
      )}
      <FieldSeparator>Variables</FieldSeparator>
      {variables?.map(variable => (
        <KeyField
          key={variable.key}
          name={`measures.${variable.key}`}
          label={variable.key}
          items={measures}
          allowEmpty
          description={`The measure that supplies ${variable.label}; empty when the source does not supply it.`}
        />
      ))}
    </EntityFormSheet>
  );
}
