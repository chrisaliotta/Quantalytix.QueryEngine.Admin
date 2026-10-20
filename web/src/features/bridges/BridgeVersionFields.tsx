import { VocabularyField } from '@/shared/form';

/** The fields of a bridge version's attributes (`BridgeVersionValues`): the cardinality and the allocation mode. */
export function BridgeVersionFields() {
  return (
    <>
      <VocabularyField name="cardinality" label="Cardinality" vocabulary="cardinality" />
      <VocabularyField name="allocationMode" label="Allocation mode" vocabulary="allocation_mode" />
    </>
  );
}
