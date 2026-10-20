import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { EntityField, KeyField, VocabularyField } from '@/shared/form';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { classificationOptions, type ClassificationValues } from './classificationOptions';

export interface ClassificationFieldsProps {
  /** The dataset whose rows are classified; null until one is chosen. */
  datasetId: number | null;
  /** The dimension the classification leads into; null until it is known. */
  targetDimensionId: number | null;
}

/**
 * The classification section of a source or route sheet, as the form's `classification`: its kind, the dataset field it
 * reads and, by kind, the mapping set or bridge that leads into the target dimension. Fields are offered from the dataset's
 * latest version; validating a release checks them in the version it pins.
 */
export function ClassificationFields({ datasetId, targetDimensionId }: ClassificationFieldsProps) {
  const { control } = useFormContext<{ classification: ClassificationValues }>();
  const [kind, fieldKey] = useWatch({ control, name: ['classification.kind', 'classification.fieldKey'] });
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const classifiedFields = useLatestVersionItems('dataset-versions', datasetId, 'classified-fields');
  const options = classificationOptions({ kind, fieldKey }, targetDimensionId, fields, classifiedFields);

  return (
    <>
      <FieldSeparator>Classification</FieldSeparator>
      <VocabularyField
        name="classification.kind"
        label="Kind"
        vocabulary="classification_kind"
        description="Direct reads member keys from the field; mapped maps the field's values through a mapping set; bridge remaps the members the field's binding groups."
      />
      <KeyField
        name="classification.fieldKey"
        label="Field"
        items={options.fields}
        description={kind === 'bridge' ? 'A field whose binding groups members of the dimension the bridge leads from.' : undefined}
      />
      {kind === 'mapped' && (
        <EntityField
          name="classification.mappingSetId"
          label="Mapping set"
          lookupKind="mapping-sets"
          filter={options.leadsIntoTarget}
          description="A mapping set into the target dimension; requests read the version their assumption set pins."
        />
      )}
      {kind === 'bridge' && (
        <EntityField
          name="classification.bridgeId"
          label="Bridge"
          lookupKind="bridges"
          parentId={options.bridgeSourceDimensionId}
          filter={options.leadsIntoTarget}
          description="A bridge from the field's dimension into the target dimension; requests read the version their assumption set pins."
        />
      )}
    </>
  );
}
