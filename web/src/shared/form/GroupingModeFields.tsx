import { useWatch } from 'react-hook-form';
import { usePublishedDimensionItems } from '../useLatestVersionItems';
import { groupingSource } from './grouping';
import { KeyField } from './KeyField';
import { NumberField } from './NumberField';
import { VocabularyField } from './VocabularyField';

export interface GroupingModeFieldsProps {
  /** The dimension whose members are grouped, the role's template; null until a role is chosen. */
  dimensionId: number | null;
}

/**
 * The default grouping mode of a calculation dimension, with the level, ancestor member or depth it groups at, as the form's
 * `groupingMode`, `groupingLevelKey`, `groupingAncestorMemberKey` and `groupingDepth`. Levels and members are named by key,
 * offered from the dimension's published version, which the active assumption set pins and requests resolve the keys in.
 */
export function GroupingModeFields({ dimensionId }: GroupingModeFieldsProps) {
  const mode = useWatch({ name: 'groupingMode' });
  const levels = usePublishedDimensionItems(dimensionId, 'levels');
  const members = usePublishedDimensionItems(dimensionId, 'members');
  const source = groupingSource(dimensionId, members);

  return (
    <>
      <VocabularyField
        name="groupingMode"
        label="Default grouping mode"
        vocabulary="grouping_mode"
        description="How runs group the role's members unless a report request names another grouping."
      />
      {mode === 'level' && <KeyField name="groupingLevelKey" label="Level" items={levels} description={source} />}
      {mode === 'ancestor' && (
        <KeyField name="groupingAncestorMemberKey" label="Ancestor member" items={members} description={source} />
      )}
      {mode === 'depth' && (
        <NumberField name="groupingDepth" label="Depth" description="The closure depth to group at; 0 groups by the member itself." />
      )}
    </>
  );
}
