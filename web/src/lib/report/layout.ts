import { isMemberKind } from './tree'
import type { GroupPresentation, RowNode } from './types'

/**
 * The placement rules for renderers (engine README, "Placing rows"): the engine returns rows in hierarchy order and a
 * renderer places them. A group is a visible member row with values and at least one child; its presentation places it:
 *
 * - `total_above`: the group row, then its children indented.
 * - `total_below`: its children indented, then the group row.
 * - `header_and_total`: a header line, its children indented, then a total line labelled `totalLabel`, else the row label.
 * - `step`: its children at the group's own indent, then the group row.
 *
 * Headings come before their children, leaves stand alone, and a hidden row is skipped with its children in its place.
 */

/** `row` is the row with its values (or a heading's label); `header` and `total` open and close a header-and-total group. */
export type StatementPart = 'row' | 'header' | 'total'

export interface StatementLine {
  key: string
  part: StatementPart
  indent: number
  label: string
  /** Negate the displayed value: the row's own flag or a member ancestor's; formula rows never inherit it, lines without values never carry it. */
  reverseSign: boolean
}

export function statementLines(rows: RowNode[], defaultPresentation: GroupPresentation): StatementLine[] {
  const place = (nodes: RowNode[], indent: number, inheritedReverse: boolean): StatementLine[] =>
    nodes.flatMap((row) => {
      const reverseSign = row.kind !== 'formula' && (inheritedReverse || !!row.reverseSign)
      const children = (at: number) => place(row.children, at, reverseSign)
      if (row.hidden) return children(indent)
      const line = (part: StatementPart, label = row.label): StatementLine => ({
        key: row.key,
        part,
        indent,
        label,
        reverseSign: reverseSign && part !== 'header' && row.kind !== 'heading',
      })
      if (row.kind === 'heading') return [line('row'), ...children(indent + 1)]
      if (!isMemberKind(row.kind) || !row.children.length) return [line('row')]
      switch (row.presentation ?? defaultPresentation) {
        case 'total_above':
          return [line('row'), ...children(indent + 1)]
        case 'total_below':
          return [...children(indent + 1), line('row')]
        case 'header_and_total':
          return [line('header'), ...children(indent + 1), line('total', row.totalLabel ?? row.label)]
        case 'step':
          return [...children(indent), line('row')]
      }
    })
  return place(rows, 0, false)
}
