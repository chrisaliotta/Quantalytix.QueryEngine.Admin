import type { LookupItem } from '@/shared/types';
import type { ClassificationInput } from './types';

/** Values of a source sheet's classification section; a choice not made yet is null. */
export interface ClassificationValues {
  kind: string | null;
  fieldKey: string | null;
  roleId: number | null;
  mappingSetId: number | null;
  bridgeId: number | null;
}

/** What the pickers of a classification offer, for its chosen role. */
export interface ClassificationOptions {
  /** The dimension whose bridges a bridge classification offers: the chosen role's template; null until a role is chosen. */
  bridgeSourceDimensionId: number | null;
  /** Keeps a mapping set or bridge that leads into the report's dimension. */
  leadsIntoTarget: (item: LookupItem) => boolean;
}

/**
 * The options of a classification into the report's dimension, which the API checks the same way when the source is saved or
 * a release is validated: a `direct` or `mapped` classification reads any field of the dataset, a `mapped` one through a
 * mapping set into the report's dimension; a `bridge` one starts from a role the dataset binds into members, through a bridge
 * from the role's template into the report's dimension.
 * @param roleId The bridge classification's chosen role.
 * @param targetDimensionId The report's dimension.
 * @param boundRoles The roles the dataset's latest version binds into members, each pointing at its template.
 */
export function classificationOptions(roleId: number | null, targetDimensionId: number | null, boundRoles: LookupItem[] | null): ClassificationOptions {
  return {
    bridgeSourceDimensionId: boundRoles?.find(role => role.id === roleId)?.parentId ?? null,
    leadsIntoTarget: item => item.parentId === targetDimensionId,
  };
}

/** The values of a stored classification, or of a new direct one when there is none. */
export function classificationValues(stored: ClassificationInput | null): ClassificationValues {
  return {
    kind: stored?.kind ?? 'direct',
    fieldKey: stored?.fieldKey ?? null,
    roleId: stored?.roleId ?? null,
    mappingSetId: stored?.mappingSetId ?? null,
    bridgeId: stored?.bridgeId ?? null,
  };
}

/** The body of the values: only the references the kind uses, because the section hides the others. */
export function toClassificationInput(values: ClassificationValues): ClassificationInput {
  const isBridge = values.kind === 'bridge';
  return {
    kind: values.kind ?? '',
    fieldKey: isBridge ? null : (values.fieldKey ?? ''),
    roleId: isBridge ? values.roleId : null,
    mappingSetId: values.kind === 'mapped' ? values.mappingSetId : null,
    bridgeId: isBridge ? values.bridgeId : null,
  };
}
