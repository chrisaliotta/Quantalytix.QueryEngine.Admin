import { describe, expect, it } from 'vitest'
import { statementLines, type StatementLine } from './layout'
import type { RowKind, RowNode } from './types'

/**
 * The engine returns rows in hierarchy order and leaves their placement to renderers; layout.ts is the one implementation of
 * the placement rules (engine README, "Placing rows"), and the Statement view shows its output. These tests pin each rule, so
 * the editor previews a statement the way every renderer lays it out.
 */

const row = (key: string, kind: RowKind, children: RowNode[] = [], extra: Partial<RowNode> = {}): RowNode => ({
  key,
  label: key,
  kind,
  hidden: false,
  children,
  ...extra,
})

/** One line per entry: the indent as two spaces per level, the part, and the label. */
const shape = (lines: StatementLine[]) => lines.map((l) => `${'  '.repeat(l.indent)}${l.part} ${l.label}`)

const group = (extra: Partial<RowNode> = {}) => [row('Total', 'subtotal', [row('A', 'line'), row('B', 'line')], extra)]

describe('statement layout', () => {
  /** `total_above`: the group row, then its children one level in. */
  it('places a total-above group before its children', () => {
    expect(shape(statementLines(group({ presentation: 'total_above' }), 'step'))).toEqual(['row Total', '  row A', '  row B'])
  })

  /** `total_below`: the children one level in, then the group row. */
  it('places a total-below group after its children', () => {
    expect(shape(statementLines(group({ presentation: 'total_below' }), 'step'))).toEqual(['  row A', '  row B', 'row Total'])
  })

  /** `header_and_total`: a header line, the children one level in, then a total line labelled with `totalLabel`. */
  it('wraps a header-and-total group in a header and a labelled total', () => {
    const lines = statementLines(group({ presentation: 'header_and_total', totalLabel: 'Total of A and B' }), 'total_above')
    expect(shape(lines)).toEqual(['header Total', '  row A', '  row B', 'total Total of A and B'])
  })

  /** Without a total label, the total line repeats the row label. */
  it('labels a header-and-total total with the row label when it has no total label', () => {
    expect(statementLines(group({ presentation: 'header_and_total' }), 'total_above').at(-1)).toMatchObject({ part: 'total', label: 'Total' })
  })

  /** `step`: the children at the group's own indent, then the group row; nested steps form a multi-step statement. */
  it('keeps step children at the group indent', () => {
    const rows = [
      row('Net', 'subtotal', [row('Gross', 'subtotal', [row('Revenue', 'line'), row('Costs', 'line')], { presentation: 'step' }), row('Tax', 'line')], {
        presentation: 'step',
      }),
    ]
    expect(shape(statementLines(rows, 'total_above'))).toEqual(['row Revenue', 'row Costs', 'row Gross', 'row Tax', 'row Net'])
  })

  /** A group with no presentation of its own follows the report default. */
  it('applies the report default to a group that inherits', () => {
    expect(shape(statementLines(group(), 'total_below'))).toEqual(['  row A', '  row B', 'row Total'])
  })

  /** A hidden row is skipped and its children take its place; headings come before their children; leaves stand alone. */
  it('skips hidden rows and keeps headings and leaves in place', () => {
    const rows = [
      row('Total', 'subtotal', [
        row('Assets', 'heading', [row('Hidden', 'line', [row('Cash', 'line'), row('Loans', 'line')], { hidden: true })]),
        row('Ratio', 'formula'),
      ]),
    ]
    expect(shape(statementLines(rows, 'total_above'))).toEqual(['row Total', '  row Assets', '    row Cash', '    row Loans', '  row Ratio'])
  })

  /** Reverse sign holds on the row and the member rows under it; formula rows and lines without values never carry it. */
  it('inherits reverse sign through member rows only', () => {
    const rows = [
      row('Expenses', 'subtotal', [row('Staff', 'heading', [row('Salaries', 'line')]), row('Cost ratio', 'formula')], {
        presentation: 'header_and_total',
        reverseSign: true,
      }),
      row('Income', 'line'),
    ]
    const signs = statementLines(rows, 'total_above').map((l) => [l.part, l.key, l.reverseSign])
    expect(signs).toEqual([
      ['header', 'Expenses', false],
      ['row', 'Staff', false],
      ['row', 'Salaries', true],
      ['row', 'Cost ratio', false],
      ['total', 'Expenses', true],
      ['row', 'Income', false],
    ])
  })
})
