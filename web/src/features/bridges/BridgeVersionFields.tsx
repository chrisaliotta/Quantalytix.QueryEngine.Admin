import { EntityField, VocabularyField } from '@/shared/form';
import type { Bridge } from './types';

/**
 * The fields of a bridge version's attributes (`BridgeVersionValues`): a version of each endpoint dimension, the
 * cardinality and the allocation mode.
 */
export function BridgeVersionFields({ bridge }: { bridge: Bridge }) {
  return (
    <>
      <EntityField
        name="sourceDimensionVersionId"
        label="Source version"
        lookupKind="dimension-versions"
        parentId={bridge.sourceDimensionId}
        description="The version of the source dimension whose members the mappings remap; publishing needs it published."
      />
      <EntityField
        name="targetDimensionVersionId"
        label="Target version"
        lookupKind="dimension-versions"
        parentId={bridge.targetDimensionId}
        description="The version of the target dimension the members map to; publishing needs it published."
      />
      <VocabularyField name="cardinality" label="Cardinality" vocabulary="cardinality" />
      <VocabularyField name="allocationMode" label="Allocation mode" vocabulary="allocation_mode" />
    </>
  );
}
