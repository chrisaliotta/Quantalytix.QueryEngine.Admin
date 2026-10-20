import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { EntityField, KeyField, VocabularyField } from '@/shared/form';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { classificationOptions, type ClassificationValues } from './classificationOptions';

export interface ClassificationFieldsProps {
  /** The dataset whose rows are classified; null until one is chosen. */
  datasetId: number | null;
  /** The report's dimension, which the classification leads into. */
  targetDimensionId: number;
}

/**
 * The classification section of a source sheet, as the form's `classification`: its kind and, by kind, the dataset field and
 * mapping set it reads, or the role and bridge a bridge classification starts from, leading into the report's dimension.
 * Fields and roles are offered from the dataset's latest version; validating a release checks them in the version it pins.
 */
export function ClassificationFields({ datasetId, targetDimensionId }: ClassificationFieldsProps) {
  const { control } = useFormContext<{ classification: ClassificationValues }>();
  const [kind, roleId] = useWatch({ control, name: ['classification.kind', 'classification.roleId'] });
  const versions = useLookup('dataset-versions', datasetId);
  const latestVersionId = versions.data?.at(-1)?.id ?? null;
  const fields = useLatestVersionItems('dataset-versions', datasetId, 'fields');
  const boundRoles = useLatestVersionItems('dataset-versions', datasetId, 'bound-roles');
  const options = classificationOptions(roleId, targetDimensionId, boundRoles);

  return (
    <>
      <FieldSeparator>Classification</FieldSeparator>
      <VocabularyField
        name="classification.kind"
        label="Kind"
        vocabulary="classification_kind"
        description="Direct reads member keys from a field; mapped maps a field's values through a mapping set; bridge remaps the members a role's binding classifies."
      />
      {kind === 'bridge' ? (
        <>
          <EntityField
            name="classification.roleId"
            label="Role"
            lookupKind="bound-roles"
            parentId={latestVersionId}
            description="A role the dataset binds into members, which the bridge remaps."
          />
          <EntityField
            name="classification.bridgeId"
            label="Bridge"
            lookupKind="bridges"
            parentId={options.bridgeSourceDimensionId}
            filter={options.leadsIntoTarget}
            description="A bridge from the role's dimension into the report's dimension; requests read the version their assumption set pins."
          />
        </>
      ) : (
        <KeyField name="classification.fieldKey" label="Field" items={fields} />
      )}
      {kind === 'mapped' && (
        <EntityField
          name="classification.mappingSetId"
          label="Mapping set"
          lookupKind="mapping-sets"
          filter={options.leadsIntoTarget}
          description="A mapping set into the report's dimension; requests read the version their assumption set pins."
        />
      )}
    </>
  );
}
