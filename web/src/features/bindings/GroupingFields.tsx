import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { KeyField, NumberField, VocabularyField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
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
  const [mode, unmapped] = useWatch({ control, name: ['groupingMode', 'unmappedBehavior'] });
  const levels = usePublishedVersionItems('dimension-versions', dimensionId, 'levels');
  const members = usePublishedVersionItems('dimension-versions', dimensionId, 'members');
  const source = groupingSource(dimensionId, members);

  return (
    <>
      <FieldSeparator>Grouping</FieldSeparator>
      <VocabularyField name="groupingMode" label="Grouping mode" vocabulary="grouping_mode" />
      {mode === 'level' && <KeyField name="groupingLevelKey" label="Level" items={levels} description={source} />}
      {mode === 'ancestor' && (
        <KeyField name="groupingAncestorMemberKey" label="Ancestor member" items={members} description={source} />
      )}
      {mode === 'depth' && (
        <NumberField name="groupingDepth" label="Depth" description="The closure depth to group at; 0 groups by the member itself." />
      )}
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
        <KeyField name="unknownMemberKey" label="Unknown member" items={members} description={source} />
      )}
    </>
  );
}

/** Says which dimension version the level and member pickers list, or why they list none. */
function groupingSource(dimensionId: number | null, members: LookupItem[] | null) {
  if (dimensionId === null) {
    return 'Choose a dimension first.';
  }
  if (members !== null && members.length === 0) {
    return 'The dimension has no published version with members yet; publish one to choose from it.';
  }
  return "From the dimension's published version; requests resolve the key in the version their assumption set pins.";
}
