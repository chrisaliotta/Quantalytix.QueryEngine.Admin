import type { CSSProperties } from 'react'
import type { ModelParameter } from '@/lib/model/types'
import type { BorderStyle, FontWeight, ReportStyle, RowNode, RowRole } from './types'

export const ROLES: RowRole[] = ['header', 'subtotal', 'total', 'detail', 'formula']
export const WEIGHTS: FontWeight[] = ['normal', 'medium', 'semibold', 'bold']
export const BORDERS: BorderStyle[] = ['none', 'thin', 'medium', 'thick', 'double']

const isMember = (r: RowNode) => r.kind !== 'heading' && r.kind !== 'formula'

interface Located {
  row: RowNode
  ancestors: RowNode[]
}

function locate(rows: RowNode[], key: string, ancestors: RowNode[] = []): Located | null {
  for (const r of rows) {
    if (r.key === key) return { row: r, ancestors }
    const found = locate(r.children, key, [...ancestors, r])
    if (found) return found
  }
  return null
}

export function totalRow(rows: RowNode[]) {
  return rows.find(isMember)
}

function hasMemberBelow(r: RowNode): boolean {
  return r.children.some((c) => isMember(c) || hasMemberBelow(c))
}

export function roleOf(rows: RowNode[], row: RowNode): RowRole {
  if (row.kind === 'heading') return 'header'
  if (row.kind === 'formula') return 'formula'
  if (totalRow(rows)?.key === row.key) return 'total'
  return hasMemberBelow(row) ? 'subtotal' : 'detail'
}

export function resolveStyle(styles: ReportStyle[], rows: RowNode[], row: RowNode) {
  const own = row.styleKey ? styles.find((s) => s.key === row.styleKey) : undefined
  if (own) return { style: own, inherited: false }
  const role = roleOf(rows, row)
  return { style: styles.find((s) => s.defaultRole === role), inherited: true }
}

export interface ResolvedValue {
  value: string | number | null
  /** Row key the value comes from, or null for the model default. */
  source: string | null
}

export function resolveParameter(rows: RowNode[], rowKey: string, param: ModelParameter, skipOwn = false): ResolvedValue {
  const fallback: ResolvedValue = { value: param.type === 'number' ? param.defaultNumber : param.defaultText, source: null }
  const loc = locate(rows, rowKey)
  if (!loc) return fallback
  const own = loc.row.parameters?.[param.key]
  if (!skipOwn && own !== undefined && own !== '') return { value: own, source: loc.row.key }

  const total = totalRow(rows)
  const fromTotal = (): ResolvedValue => {
    const v = total?.parameters?.[param.key]
    return total && total.key !== loc.row.key && v !== undefined && v !== '' ? { value: v, source: total.key } : fallback
  }

  if (loc.row.kind === 'formula') return fromTotal()
  for (const a of [...loc.ancestors].reverse()) {
    const v = a.parameters?.[param.key]
    if (v !== undefined && v !== '') return { value: v, source: a.key }
  }
  if (loc.row.kind === 'heading' && loc.ancestors.length === 0) return fromTotal()
  return fallback
}

const WEIGHT_CSS: Record<FontWeight, number> = { normal: 400, medium: 500, semibold: 600, bold: 700 }
const BORDER_CSS: Record<BorderStyle, string> = {
  none: 'none',
  thin: '1px solid',
  medium: '2px solid',
  thick: '3px solid',
  double: '3px double',
}

/** Engine rendering of a style; empty fields keep the viewer's defaults. */
export function styleCss(s: ReportStyle | undefined): CSSProperties {
  if (!s) return {}
  const css: CSSProperties = {}
  if (s.fontFamily) css.fontFamily = s.fontFamily
  if (s.fontSize) css.fontSize = `${s.fontSize}pt`
  if (s.fontWeight) css.fontWeight = WEIGHT_CSS[s.fontWeight]
  if (s.italic) css.fontStyle = 'italic'
  if (s.textColor) css.color = s.textColor
  if (s.backgroundColor) css.backgroundColor = s.backgroundColor
  if (s.borderTop && s.borderTop !== 'none') css.borderTop = `${BORDER_CSS[s.borderTop]} currentColor`
  if (s.borderBottom && s.borderBottom !== 'none') css.borderBottom = `${BORDER_CSS[s.borderBottom]} currentColor`
  return css
}

export function styleUsers(rows: RowNode[], key: string): RowNode[] {
  const out: RowNode[] = []
  const walk = (list: RowNode[]) => list.forEach((r) => (r.styleKey === key && out.push(r), walk(r.children)))
  walk(rows)
  return out
}
