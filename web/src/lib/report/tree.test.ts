import { describe, expect, it } from 'vitest'
import { canWrapRows, siblingRange, wrapRows } from './tree'
import type { RowKind, RowNode } from './types'

/**
 * "Subtotal these rows" turns a shift-click range of siblings into a new subtotal over them. These tests confirm the tree
 * rules behind it: which rows a range covers, which ranges a subtotal can wrap, and where the wrapped rows end up.
 */

const row = (key: string, kind: RowKind = 'line', children: RowNode[] = []): RowNode => ({ key, label: key, kind, hidden: false, children })

const statement = () => [
  row('Total', 'subtotal', [row('Revenue'), row('Costs'), row('Tax'), row('Notes', 'heading', [row('A'), row('B')])]),
  row('Ratio', 'formula'),
]

const keysOf = (rows: RowNode[]): unknown => rows.map((r) => (r.children.length ? { [r.key]: keysOf(r.children) } : r.key))

describe('row ranges and wrapping', () => {
  /** A shift-click range runs between the two rows in tree order, whichever was clicked first. */
  it('covers the siblings between two rows in either direction', () => {
    expect(siblingRange(statement(), 'Tax', 'Revenue')).toEqual(['Revenue', 'Costs', 'Tax'])
  })

  /** A range never crosses levels: rows under different parents are no range. */
  it('has no range across parents', () => {
    expect(siblingRange(statement(), 'Revenue', 'A')).toBeNull()
  })

  /** A subtotal is a report dimension member, so it can only wrap two or more consecutive siblings under a member row. */
  it('wraps only consecutive siblings under a member row', () => {
    const rows = statement()
    expect(canWrapRows(rows, ['Revenue', 'Costs'])).toBe(true)
    expect(canWrapRows(rows, ['Revenue', 'Tax'])).toBe(false)
    expect(canWrapRows(rows, ['Revenue'])).toBe(false)
    expect(canWrapRows(rows, ['A', 'B'])).toBe(false)
    expect(canWrapRows(rows, ['Total', 'Ratio'])).toBe(false)
  })

  /** The new row takes the range's place among its siblings and the range moves under it, in order. */
  it('puts the wrapped rows under the new row in their place', () => {
    const rows = statement()
    wrapRows(rows, ['Revenue', 'Costs'], row('Gross', 'subtotal'))
    expect(keysOf(rows)).toEqual([{ Total: [{ Gross: ['Revenue', 'Costs'] }, 'Tax', { Notes: ['A', 'B'] }] }, 'Ratio'])
  })
})
