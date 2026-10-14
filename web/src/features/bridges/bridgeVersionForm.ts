import type { BridgeVersion, BridgeVersionInput } from './types';

/** Values of the bridge version fields. */
export interface BridgeVersionValues {
  sourceDimensionVersionId: number | null;
  targetDimensionVersionId: number | null;
  cardinality: string | null;
  allocationMode: string | null;
}

/** A stored version's attributes as field values. */
export function bridgeVersionValues(version: BridgeVersion): BridgeVersionValues {
  return {
    sourceDimensionVersionId: version.sourceDimensionVersionId,
    targetDimensionVersionId: version.targetDimensionVersionId,
    cardinality: version.cardinality,
    allocationMode: version.allocationMode,
  };
}

/** The field values as the API takes them; a missing choice is sent empty, so the API names it on its field. */
export function toBridgeVersionInput(values: BridgeVersionValues): BridgeVersionInput {
  return {
    sourceDimensionVersionId: values.sourceDimensionVersionId ?? 0,
    targetDimensionVersionId: values.targetDimensionVersionId ?? 0,
    cardinality: values.cardinality ?? '',
    allocationMode: values.allocationMode ?? '',
  };
}
