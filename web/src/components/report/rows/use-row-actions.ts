import { useCallback, useMemo } from 'react'
import { toast } from 'sonner'
import { useUndoAction } from '@/components/shell/history'
import { useEditor } from '@/lib/report/store'
import {
  allRows,
  deleteRowKeepChildren,
  findRow,
  flatten,
  indentRow,
  insertRow,
  locate,
  moveAmongSiblings,
  outdentRow,
  removeRow,
  setRowKind,
  toRowKey,
  uniqueKey,
  wrapRows,
} from '@/lib/report/tree'
import type { RowKind, RowNode } from '@/lib/report/types'

export function focusRow(key: string) {
  requestAnimationFrame(() => {
    document.querySelector<HTMLElement>(`[data-row-key="${CSS.escape(key)}"]`)?.focus()
  })
}

export function useRowActions() {
  const { report, ui, mutate, setReport, setUi } = useEditor()
  const undoAction = useUndoAction()

  const expand = useCallback(
    (key: string | null) => {
      if (key) setUi((u) => ({ collapsed: u.collapsed.filter((k) => k !== key) }))
    },
    [setUi],
  )

  const newRow = useCallback(
    (kind: RowKind, label: string): RowNode => ({
      key: uniqueKey(toRowKey(label), allRows(report.rows).map((r) => r.key)),
      label: label.trim(),
      kind,
      hidden: false,
      children: [],
    }),
    [report.rows],
  )

  const create = useCallback(
    (parentKey: string | null, index: number, kind: RowKind, label: string) => {
      const node = newRow(kind, label)
      mutate((d) => insertRow(d.rows, parentKey, index, node))
      expand(parentKey)
      setUi({ selectedRowKey: node.key })
      focusRow(node.key)
      return node.key
    },
    [newRow, mutate, expand, setUi],
  )

  return useMemo(
    () => ({
      create,
      /** Wraps consecutive sibling rows in a new subtotal shown as a step: the rows, then its total. */
      subtotal: (keys: string[], label: string) => {
        const node: RowNode = { ...newRow('subtotal', label), presentation: 'step' }
        mutate((d) => wrapRows(d.rows, keys, node), `Subtotal ${keys.length} rows`)
        setUi({ selectedRowKey: node.key })
        focusRow(node.key)
      },
      rename: (key: string, label: string) => {
        if (!label.trim()) return
        mutate((d) => {
          const row = findRow(d.rows, key)
          if (row) row.label = label.trim()
        })
      },
      setKind: (key: string, kind: RowKind) =>
        mutate((d) => {
          const row = findRow(d.rows, key)
          if (row) setRowKind(row, kind)
        }),
      toggleHidden: (key: string) =>
        mutate((d) => {
          const row = findRow(d.rows, key)
          if (row) row.hidden = !row.hidden
        }),
      move: (key: string, delta: -1 | 1) => {
        mutate((d) => {
          moveAmongSiblings(d.rows, key, delta)
        })
        focusRow(key)
      },
      indent: (key: string) => {
        const loc = locate(report.rows, key)
        if (!loc || loc.index === 0) return
        expand(loc.siblings[loc.index - 1].key)
        mutate((d) => {
          indentRow(d.rows, key)
        })
        focusRow(key)
      },
      outdent: (key: string) => {
        mutate((d) => {
          outdentRow(d.rows, key)
        })
        focusRow(key)
      },
      moveTo: (key: string, parentKey: string | null, index: number) => {
        mutate((d) => {
          const node = removeRow(d.rows, key)
          if (node) insertRow(d.rows, parentKey, index, node)
        })
        expand(parentKey)
      },
      remove: (key: string) => {
        const visible = flatten(report.rows, new Set(ui.collapsed))
        const at = visible.findIndex((r) => r.node.key === key)
        const label = findRow(report.rows, key)?.label ?? key
        const before = report
        mutate((d) => {
          const parentKey = locate(d.rows, key)?.parent?.key
          deleteRowKeepChildren(d.rows, key)
          for (const s of d.sources) s.mappings = s.mappings.filter((m) => m.rowKey !== key)
          d.rowScopes = d.rowScopes.filter((s) => s.rowKey !== key)
          for (const r of allRows(d.rows)) if (r.scopeKey === key) r.scopeKey = parentKey
        })
        const neighbor = visible[at + 1]?.node.key ?? visible[at - 1]?.node.key ?? null
        setUi({ selectedRowKey: neighbor === key ? null : neighbor, dialog: null })
        if (neighbor) focusRow(neighbor)
        toast(`Deleted "${label}"`, { action: undoAction() })
      },
    }),
    [create, newRow, mutate, setReport, report, ui.collapsed, expand, setUi],
  )
}

export type RowActions = ReturnType<typeof useRowActions>
