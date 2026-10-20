import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { GroupingModeFields, groupingSource, KeyField, VocabularyField } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import type { BindingFormValues } from './bindingForm';

export interface GroupingFieldsProps {
  /** The binding's dimension; null until one is chosen. */
  dimensionId: number | null;
}

/**
 * The binding sheet's grouping section: the grouping mode with its level, ancestor member or depth, the ragged and period
 * alignment behaviors, and the unmapped behavior with its unknown member. Levels and members are named by key, offered
 * from the dimension's published version, which the active assumption set pins and requests resolve the keys in.
 */
export function GroupingFields({ dimensionId }: GroupingFieldsProps) {
  const { control } = useFormContext<BindingFormValues>();
  const unmapped = useWatch({ control, name: 'unmappedBehavior' });
  const members = usePublishedVersionItems('dimension-versions', dimensionId, 'members');

  return (
    <>
      <FieldSeparator>Grouping</FieldSeparator>
      <GroupingModeFields dimensionId={dimensionId} />
      <VocabularyField
        name="raggedBehavior"
        label="Ragged members"
        vocabulary="ragged_behavior"
        description="What grouping does with a member that has no ancestor at the requested level or depth."
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
