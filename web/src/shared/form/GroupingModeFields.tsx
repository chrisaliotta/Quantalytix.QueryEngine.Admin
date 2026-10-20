import { useWatch } from 'react-hook-form';
import { usePublishedVersionItems } from '../useLatestVersionItems';
import { groupingSource } from './grouping';
import { KeyField } from './KeyField';
import { NumberField } from './NumberField';
import { VocabularyField } from './VocabularyField';

export interface GroupingModeFieldsProps {
  /** The dimension whose members are grouped; null until one is chosen. */
  dimensionId: number | null;
}

/**
 * The grouping mode of a binding or calculation dimension, with the level, ancestor member or depth it groups at, as the
 * form's `groupingMode`, `groupingLevelKey`, `groupingAncestorMemberKey` and `groupingDepth`. Levels and members are named by
 * key, offered from the dimension's published version, which the active assumption set pins and requests resolve the keys in.
 */
export function GroupingModeFields({ dimensionId }: GroupingModeFieldsProps) {
  const mode = useWatch({ name: 'groupingMode' });
  const levels = usePublishedVersionItems('dimension-versions', dimensionId, 'levels');
  const members = usePublishedVersionItems('dimension-versions', dimensionId, 'members');
  const source = groupingSource(dimensionId, members);

  return (
    <>
      <VocabularyField name="groupingMode" label="Grouping mode" vocabulary="grouping_mode" />
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
