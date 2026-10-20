import { useForm, useWatch } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, KeyField } from '@/shared/form';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useSaveVersionContent, useVersionContent } from '@/shared/versionContent';
import { ClassificationFields } from './ClassificationFields';
import { classificationValues, toClassificationInput, type ClassificationValues } from './classificationOptions';
import type { CalculationRoute } from './types';

/** Values of the route sheet; a choice not made yet is null. */
interface CalculationRouteValues {
  calculationDimensionId: number | null;
  datasetId: number | null;
  variableSourceId: number | null;
  classification: ClassificationValues;
}

export interface CalculationRouteSheetProps {
  versionId: number;
  /** The route to edit or view; null to create one. */
  route: CalculationRoute | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a route: how one dataset's rows classify into a calculation dimension, for every source of the
 * dataset or, as an override, for one source. A canonical dimension's route classifies into its dimension, checked when it
 * is saved; a scalar dimension's route reads a field's own values.
 */
export function CalculationRouteSheet({ versionId, route, readOnly, onClose }: CalculationRouteSheetProps) {
  const save = useSaveVersionContent('routes', versionId, route?.calculationBindingId ?? null);
  const calculationDimensions = useVersionContent('calculation-dimensions', versionId);
  const form = useForm<CalculationRouteValues>({
    defaultValues: {
      calculationDimensionId: route?.calculationDimensionId ?? null,
      datasetId: route?.datasetId ?? null,
      variableSourceId: route?.variableSourceId ?? null,
      classification: classificationValues(
        route && { kind: route.routeKind, fieldKey: route.fieldKey, mappingSetId: route.mappingSetId, bridgeId: route.bridgeId }
      ),
    },
  });
  const [calculationDimensionId, datasetId] = useWatch({ control: form.control, name: ['calculationDimensionId', 'datasetId'] });
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const target = calculationDimensions.data?.find(dimension => dimension.calculationDimensionId === calculationDimensionId);
  const isScalar = target?.valueKind === 'scalar';

  function submit(values: CalculationRouteValues) {
    return save({
      calculationDimensionId: values.calculationDimensionId ?? 0,
      datasetId: values.datasetId ?? 0,
      variableSourceId: values.variableSourceId,
      classification: toClassificationInput(isScalar ? { ...values.classification, kind: 'field' } : values.classification),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('route', route?.fieldKey ?? null, readOnly)}
      description="Validating a release checks the route's field and classification in the dataset version it pins."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
      width="36rem"
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
      {isScalar ? (
        <KeyField
          name="classification.fieldKey"
          label="Field"
          items={fields}
          description="A scalar dimension reads the field's own values, so its routes are field routes."
        />
      ) : (
        <ClassificationFields datasetId={datasetId} targetDimensionId={target?.dimensionId ?? null} />
      )}
    </EntityFormSheet>
  );
}
