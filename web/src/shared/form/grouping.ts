import type { LookupItem } from '../types';

/** The grouping values of a binding or calculation dimension sheet; a choice not made yet is null. */
export interface GroupingValues {
  groupingMode: string | null;
  groupingLevelKey: string | null;
  groupingAncestorMemberKey: string | null;
  groupingDepth: number | null;
}

/** The grouping references of the body: only the one the mode uses, because the section hides the others. */
export function toGroupingReferences(values: GroupingValues) {
  return {
    groupingLevelKey: values.groupingMode === 'level' ? values.groupingLevelKey : null,
    groupingAncestorMemberKey: values.groupingMode === 'ancestor' ? values.groupingAncestorMemberKey : null,
    groupingDepth: values.groupingMode === 'depth' ? values.groupingDepth : null,
  };
}

/** Says which dimension version the level and member pickers of a grouping list, or why they list none. */
export function groupingSource(dimensionId: number | null, members: LookupItem[] | null) {
  if (dimensionId === null) {
    return 'Choose a dimension first.';
  }
  if (members !== null && members.length === 0) {
    return 'The dimension has no published version with members yet; publish one to choose from it.';
  }
  return "From the dimension's published version; requests resolve the key in the version their assumption set pins.";
}
