import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, KeyField, VocabularyField } from '@/shared/form';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useSaveModelContent } from './api';
import { contentSheetTitle } from './contentSheet';
import type { CalculationRoute } from './types';

/** Values of the route sheet; a choice not made yet is null. */
interface CalculationRouteValues {
  calculationDimensionId: number | null;
  datasetId: number | null;
  variableSourceId: number | null;
  routeKind: string | null;
  fieldKey: string | null;
}

export interface CalculationRouteSheetProps {
  versionId: number;
  /** The route to edit or view; null to create one. */
  route: CalculationRoute | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a route: the field of one dataset that produces a calculation dimension, for every source of
 * the dataset or, as an override, for one source.
 */
export function CalculationRouteSheet({ versionId, route, readOnly, onClose }: CalculationRouteSheetProps) {
  const save = useSaveModelContent('routes', versionId, route?.calculationBindingId ?? null);
  const form = useForm<CalculationRouteValues>({
    defaultValues: {
      calculationDimensionId: route?.calculationDimensionId ?? null,
      datasetId: route?.datasetId ?? null,
      variableSourceId: route?.variableSourceId ?? null,
      routeKind: route?.routeKind ?? 'semantic',
      fieldKey: route?.fieldKey ?? null,
    },
  });
  const datasetId = useWatch({ control: form.control, name: 'datasetId' });
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');

  function submit(values: CalculationRouteValues) {
    return save({
      calculationDimensionId: values.calculationDimensionId ?? 0,
      datasetId: values.datasetId ?? 0,
      variableSourceId: values.variableSourceId,
      routeKind: values.routeKind ?? '',
      fieldKey: values.fieldKey ?? '',
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('route', route?.fieldKey ?? null, readOnly)}
      description="Validating a release checks that the field produces the dimension in the dataset version it pins."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <EntityField name="calculationDimensionId" label="Dimension" lookupKind="calculation-dimensions" parentId={versionId} />
      <EntityField name="datasetId" label="Dataset" lookupKind="datasets" />
      <EntityField
        name="variableSourceId"
        label="For source"
        lookupKind="variable-sources"
        parentId={versionId}
        allowEmpty
        description="Optional: the one source of the dataset the route overrides the default for."
      />
      <VocabularyField
        name="routeKind"
        label="Kind"
        vocabulary="route_kind"
        description="Semantic routes read a field's canonical binding; field routes read the field's own values."
      />
      <KeyField name="fieldKey" label="Field" items={fields} />
    </EntityFormSheet>
  );
}
