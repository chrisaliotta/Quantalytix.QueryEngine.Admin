import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { groupingSource, KeyField, VocabularyField } from '@/shared/form';
import { usePublishedDimensionItems } from '@/shared/useLatestVersionItems';
import type { BindingFormValues } from './bindingForm';

export interface MemberBehaviorFieldsProps {
  /** The binding's dimension: its role's template, or a time binding's dimension; null until one is chosen. */
  dimensionId: number | null;
}

/**
 * The binding sheet's member behaviors: what grouping a request does with ragged members, the period alignment, and the
 * unmapped behavior with its unknown member. The member is named by key, offered from the dimension's published version,
 * which the active assumption set pins and requests resolve the key in.
 */
export function MemberBehaviorFields({ dimensionId }: MemberBehaviorFieldsProps) {
  const { control } = useFormContext<BindingFormValues>();
  const unmapped = useWatch({ control, name: 'unmappedBehavior' });
  const members = usePublishedDimensionItems(dimensionId, 'members');

  return (
    <>
      <FieldSeparator>Members</FieldSeparator>
      <VocabularyField
        name="raggedBehavior"
        label="Ragged members"
        vocabulary="ragged_behavior"
        description="What a request that groups at a level or depth does with a member that has no ancestor there."
      />
      <VocabularyField name="defaultPeriodAlignment" label="Period alignment" vocabulary="default_period_alignment" />
      <VocabularyField
        name="unmappedBehavior"
        label="Unmapped values"
        vocabulary="unmapped_behavior"
        description="What happens to rows whose source value maps to no member."
      />
      {unmapped === 'unknown' && (
        <KeyField name="unknownMemberKey" label="Unknown member" items={members} description={groupingSource(dimensionId, members)} />
      )}
    </>
  );
}
