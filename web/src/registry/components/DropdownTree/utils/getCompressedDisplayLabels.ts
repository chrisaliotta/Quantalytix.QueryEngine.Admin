import type { DropdownTreeItem } from '../types';

const ALL_SUFFIX = ' (All)';

function formatAllLabel(label: string): string {
  return `${label}${ALL_SUFFIX}`;
}

/**
 * Build trigger labels for checkable multi-select: collapse fully selected subtrees
 * to "Parent (All)" and omit selected descendants.
 *
 * Works when the controlled value contains only leaf ids (initial load) or
 * includes bubbled parent ids after cascade selection.
 */
export function getCompressedDisplayLabels<T>(
  nodes: DropdownTreeItem<T>[],
  selectedIds: Set<string>,
  descendantsMap: Map<string, string[]>
): string[] {
  const labels: string[] = [];

  function walk(list: DropdownTreeItem<T>[]) {
    for (const node of list) {
      const descendants = descendantsMap.get(node.id) ?? [];

      if (descendants.length > 0) {
        const allDescendantsSelected = descendants.every(id => selectedIds.has(id));
        const anyDescendantSelected = descendants.some(id => selectedIds.has(id));
        const parentDirectlySelected = selectedIds.has(node.id);

        if (
          (allDescendantsSelected && anyDescendantSelected) ||
          parentDirectlySelected
        ) {
          labels.push(formatAllLabel(node.label));
          continue;
        }

        if (node.children?.length) walk(node.children);
        continue;
      }

      if (selectedIds.has(node.id)) {
        labels.push(node.label);
      }
    }
  }

  walk(nodes);
  return labels;
}
