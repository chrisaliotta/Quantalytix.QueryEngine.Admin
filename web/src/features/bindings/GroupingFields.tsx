import { useFormContext, useWatch } from 'react-hook-form';
import { FieldSeparator } from '@/components/ui/field';
import { EntityField, NumberField, VocabularyField } from '@/shared/form';
import type { BindingFormValues } from './bindingForm';
import { useGroupingVersion, type GroupingVersion } from './queries';

export interface GroupingFieldsProps {
  /** The binding's dimension; null until one is chosen. */
  dimensionId: number | null;
  /** The binding's pinned dimension version; null resolves the version in effect now. */
  pinnedVersionId: number | null;
}

/**
 * The binding sheet's grouping section: the grouping mode with its level, ancestor member or depth, the ragged and period
 * alignment behaviors, and the unmapped behavior with its unknown member. Levels and members come from the pinned
 * version, or from the version of the dimension in effect now when the binding pins none.
 */
export function GroupingFields({ dimensionId, pinnedVersionId }: GroupingFieldsProps) {
  const { control } = useFormContext<BindingFormValues>();
  const [mode, unmapped] = useWatch({ control, name: ['groupingMode', 'unmappedBehavior'] });
  const grouping = useGroupingVersion(dimensionId, pinnedVersionId);
  const source = groupingSource(dimensionId, pinnedVersionId, grouping);

  return (
    <>
      <FieldSeparator>Grouping</FieldSeparator>
      <VocabularyField name="groupingMode" label="Grouping mode" vocabulary="grouping_mode" />
      {mode === 'level' && (
        <EntityField name="groupingLevelId" label="Level" lookupKind="levels" parentId={grouping.versionId} description={source} />
      )}
      {mode === 'ancestor' && (
        <EntityField
          name="groupingAncestorMemberId"
          label="Ancestor member"
          lookupKind="members"
          parentId={grouping.versionId}
          description={source}
        />
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
        <EntityField name="unknownMemberId" label="Unknown member" lookupKind="members" parentId={grouping.versionId} description={source} />
      )}
    </>
  );
}

/** Says which dimension version the level and member pickers list, or why they list none. */
function groupingSource(dimensionId: number | null, pinnedVersionId: number | null, grouping: GroupingVersion) {
  if (dimensionId === null) {
    return 'Choose a dimension first.';
  }
  if (pinnedVersionId !== null) {
    return 'From the pinned version.';
  }
  if (grouping.effective) {
    return `From v${grouping.effective.versionNumber}, the version of the dimension in effect now.`;
  }
  if (grouping.isPending) {
    return 'Finding the version in effect now...';
  }
  return 'The dimension has no published version in effect now; pin a version to choose from it.';
}
