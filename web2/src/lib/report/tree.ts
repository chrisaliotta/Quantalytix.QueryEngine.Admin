import type { RowNode } from './types'

export interface FlatRow {
  node: RowNode
  depth: number
  parentKey: string | null
}

export function flatten(rows: RowNode[], collapsed: Set<string> = new Set(), depth = 0, parentKey: string | null = null): FlatRow[] {
  const out: FlatRow[] = []
  for (const node of rows) {
    out.push({ node, depth, parentKey })
    if (node.children.length && !collapsed.has(node.key)) {
      out.push(...flatten(node.children, collapsed, depth + 1, node.key))
    }
  }
  return out
}

export interface Located {
  node: RowNode
  parent: RowNode | null
  siblings: RowNode[]
  index: number
}

export function locate(rows: RowNode[], key: string, parent: RowNode | null = null): Located | null {
  for (let i = 0; i < rows.length; i++) {
    if (rows[i].key === key) return { node: rows[i], parent, siblings: rows, index: i }
    const found = locate(rows[i].children, key, rows[i])
    if (found) return found
  }
  return null
}

export function findRow(rows: RowNode[], key: string) {
  return locate(rows, key)?.node ?? null
}

export function allRows(rows: RowNode[]): RowNode[] {
  return rows.flatMap((r) => [r, ...allRows(r.children)])
}

export function descendantKeys(node: RowNode): string[] {
  return node.children.flatMap((c) => [c.key, ...descendantKeys(c)])
}

export function ancestorKeys(rows: RowNode[], key: string): string[] {
  const out: string[] = []
  let loc = locate(rows, key)
  while (loc?.parent) {
    out.push(loc.parent.key)
    loc = locate(rows, loc.parent.key)
  }
  return out
}

export function toRowKey(label: string) {
  return label
    .trim()
    .replace(/[^A-Za-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .toLowerCase()
    .replace(/^(?=\d)/, 'row_')
}

export function uniqueKey(base: string, existing: string[]) {
  const taken = new Set(existing.map((k) => k.toLowerCase()))
  const root = base || 'row'
  if (!taken.has(root.toLowerCase())) return root
  let n = 2
  while (taken.has(`${root}_${n}`.toLowerCase())) n++
  return `${root}_${n}`
}

export function removeRow(rows: RowNode[], key: string): RowNode | null {
  const loc = locate(rows, key)
  if (!loc) return null
  loc.siblings.splice(loc.index, 1)
  return loc.node
}

export function insertRow(rows: RowNode[], parentKey: string | null, index: number, node: RowNode) {
  const siblings = parentKey ? findRow(rows, parentKey)?.children : rows
  if (!siblings) return
  siblings.splice(Math.max(0, Math.min(index, siblings.length)), 0, node)
}

/** Removes a row and lifts its children into its place. */
export function deleteRowKeepChildren(rows: RowNode[], key: string) {
  const loc = locate(rows, key)
  if (!loc) return
  loc.siblings.splice(loc.index, 1, ...loc.node.children)
}

export function moveAmongSiblings(rows: RowNode[], key: string, delta: -1 | 1) {
  const loc = locate(rows, key)
  if (!loc) return false
  const target = loc.index + delta
  if (target < 0 || target >= loc.siblings.length) return false
  const [node] = loc.siblings.splice(loc.index, 1)
  loc.siblings.splice(target, 0, node)
  return true
}

export function indentRow(rows: RowNode[], key: string) {
  const loc = locate(rows, key)
  if (!loc || loc.index === 0) return null
  const newParent = loc.siblings[loc.index - 1]
  loc.siblings.splice(loc.index, 1)
  newParent.children.push(loc.node)
  return newParent.key
}

export function outdentRow(rows: RowNode[], key: string) {
  const loc = locate(rows, key)
  if (!loc?.parent) return false
  const parentLoc = locate(rows, loc.parent.key)
  if (!parentLoc) return false
  loc.siblings.splice(loc.index, 1)
  parentLoc.siblings.splice(parentLoc.index + 1, 0, loc.node)
  return true
}
