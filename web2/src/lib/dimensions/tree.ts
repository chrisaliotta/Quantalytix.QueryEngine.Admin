import type { DimensionMember } from './contract'

export interface TreeIndex {
  byId: Map<string, DimensionMember>
  /** Children keyed by parent id; roots under `null`. Sorted by sortOrder. */
  children: Map<string | null, DimensionMember[]>
  depth: Map<string, number>
  /** Members not reachable from a root: their parent links form a cycle. */
  unreachable: DimensionMember[]
}

export function indexTree(members: DimensionMember[]): TreeIndex {
  const byId = new Map(members.map((m) => [m.id, m]))
  const children = new Map<string | null, DimensionMember[]>()
  for (const m of members) {
    const p = m.parentId && byId.has(m.parentId) ? m.parentId : null
    const list = children.get(p) ?? []
    list.push(m)
    children.set(p, list)
  }
  for (const list of children.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.key.localeCompare(b.key))
  const depth = new Map<string, number>()
  const walk = (id: string | null, d: number) => {
    for (const c of children.get(id) ?? []) {
      if (depth.has(c.id)) continue
      depth.set(c.id, d)
      walk(c.id, d + 1)
    }
  }
  walk(null, 0)
  const unreachable = members.filter((m) => !depth.has(m.id))
  return { byId, children, depth, unreachable }
}

export function descendantIds(index: TreeIndex, id: string) {
  const out = new Set<string>()
  const stack = [id]
  while (stack.length) {
    const cur = stack.pop()!
    for (const c of index.children.get(cur) ?? []) {
      if (out.has(c.id)) continue
      out.add(c.id)
      stack.push(c.id)
    }
  }
  return out
}

export function ancestorPath(index: TreeIndex, id: string) {
  const path: DimensionMember[] = []
  const seen = new Set<string>()
  let cur = index.byId.get(id)?.parentId ?? null
  while (cur && !seen.has(cur)) {
    seen.add(cur)
    const m = index.byId.get(cur)
    if (!m) break
    path.unshift(m)
    cur = m.parentId
  }
  return path
}

/** Each parent-link cycle as a list of member ids. */
export function findCycles(members: DimensionMember[]) {
  const byId = new Map(members.map((m) => [m.id, m]))
  const state = new Map<string, 'visiting' | 'done'>()
  const cycles: string[][] = []
  for (const start of members) {
    const path: string[] = []
    let cur: string | null = start.id
    while (cur && byId.has(cur) && !state.has(cur)) {
      state.set(cur, 'visiting')
      path.push(cur)
      cur = byId.get(cur)!.parentId
    }
    if (cur && state.get(cur) === 'visiting') cycles.push(path.slice(path.indexOf(cur)))
    for (const id of path) state.set(id, 'done')
  }
  return cycles
}
