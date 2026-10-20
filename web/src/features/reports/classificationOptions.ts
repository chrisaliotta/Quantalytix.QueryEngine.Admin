import type { LookupItem } from '@/shared/types';
import type { ClassificationInput } from './types';

/** Values of a sheet's classification section; a choice not made yet is null. */
export interface ClassificationValues {
  kind: string | null;
  fieldKey: string | null;
  mappingSetId: number | null;
  bridgeId: number | null;
}

/** What the pickers of a classification offer, for its kind and chosen field. */
export interface ClassificationOptions {
  /** The fields the classification can read; null while they load. */
  fields: LookupItem[] | null;
  /** The dimension whose bridges a bridge classification offers: the one the chosen field's binding groups; null until then. */
  bridgeSourceDimensionId: number | null;
  /** Keeps a mapping set or bridge that leads into the target dimension. */
  leadsIntoTarget: (item: LookupItem) => boolean;
}

/**
 * The options of a classification into a target dimension, which the API checks the same way when the classification is
 * saved: a `direct` or `mapped` classification reads any field of the dataset, a `mapped` one through a mapping set into
 * the target, and a `bridge` one reads a field whose binding groups members of a dimension, through a bridge from that
 * dimension into the target.
 * @param fields The fields of the dataset's latest version.
 * @param classifiedFields Its fields whose binding groups members, each pointing at the dimension it groups.
 */
export function classificationOptions(
  classification: Pick<ClassificationValues, 'kind' | 'fieldKey'>,
  targetDimensionId: number | null,
  fields: LookupItem[] | null,
  classifiedFields: LookupItem[] | null
): ClassificationOptions {
  return {
    fields: classification.kind === 'bridge' ? classifiedFields : fields,
    bridgeSourceDimensionId: classifiedFields?.find(field => field.key === classification.fieldKey)?.parentId ?? null,
    leadsIntoTarget: item => item.parentId === targetDimensionId,
  };
}

/** The values of a stored classification, or of a new direct one when there is none. */
export function classificationValues(stored: ClassificationInput | null): ClassificationValues {
  return {
    kind: stored?.kind ?? 'direct',
    fieldKey: stored?.fieldKey ?? null,
    mappingSetId: stored?.mappingSetId ?? null,
    bridgeId: stored?.bridgeId ?? null,
  };
}

/** The body of the values: only the reference the kind uses, because the section hides the other one. */
export function toClassificationInput(values: ClassificationValues): ClassificationInput {
  return {
    kind: values.kind ?? '',
    fieldKey: values.fieldKey ?? '',
    mappingSetId: values.kind === 'mapped' ? values.mappingSetId : null,
    bridgeId: values.kind === 'bridge' ? values.bridgeId : null,
  };
}
