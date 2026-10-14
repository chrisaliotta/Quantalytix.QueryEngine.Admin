## DataTable (registry/v2)

Headless-but-ergonomic table built on TanStack Table. Defaults render through shadcn `<Table>` primitives, but every surface can be overridden via render props and per-column meta renderers.

### Files
- `DataTable.tsx`: component + types for renderers and column meta.
- `DataTable.stories.tsx`: Storybook examples (default, custom render props, sortable headers, grouped headers, custom empty state).

### Structure & logic
- Creates a TanStack table with sorting + column filters. `filterValue` (if provided) is applied to column id `description` (matches existing consumers).
- State: `sorting`, `columnFilters` managed internally; controlled via TanStack callbacks.
- Layout: outer container flex column; inner scroll area; sticky header/footer; `table` uses `tableLayout: auto`.
- Rendering pipeline:
  - Renderers (table/container/header/body/footer + rows/cells) fall back to shadcn primitives.
  - Column meta renderers (`meta.renderHeaderCell`, `meta.renderCell`) run before table-level renderers and can wrap/replace per-column content.
  - Empty state is customizable; default shows “No results.”

### Styling defaults
- Minimal structural styles only: scroll area, sticky header/footer, and shadcn table baseline (padding, typography). No theme-specific borders/colors beyond shadcn defaults.
- Consumers can:
  - Override renderers to swap elements/styles.
  - Use column meta to style individual headers/cells.
  - Apply `className` on the outer container for layout.

### Props (key)
- `columns: DataTableColumnDef<T>` — TanStack columns; supports `meta` for per-column renderers.
- `data: T[]` — rows.
- `filterValue?: string` — applied to `description` column filter when present.
- `className?: string` — applied to outer container.
- `renderers?: DataTableRenderers<T>` — headless render overrides (container, scroll, table, header/footer, rows, cells, empty).

### Column meta helpers
Each column `meta` can include:
- `renderHeaderCell(args)` — wrap/replace a header cell.
- `renderCell(args)` — wrap/replace a body cell.
- `isSpacer?: boolean` — mark a blank spacer column (zero padding, narrow width) to add visual gaps between grouped headers.

Both receive `defaultProps` (includes `key`) and `content` already resolved via `flexRender`.

### Renderers (table-level)
`renderContainer`, `renderScrollArea`, `renderTable`, `renderHeader`, `renderHeaderRow`, `renderHeaderCell`, `renderBody`, `renderRow`, `renderCell`, `renderEmptyState`, `renderFooter`, `renderFooterRow`, `renderFooterCell`.
Each gets minimal `defaultProps` for convenience; return JSX to override.

### Usage snippets
**Basic (defaults to shadcn):**
```tsx
const columns: DataTableColumnDef<Person>[] = [
  { accessorKey: "name", header: "Name" },
  { accessorKey: "title", header: "Title" },
  { accessorKey: "email", header: "Email" },
]

<DataTable columns={columns} data={people} />
```

**Column-level styling via meta:**
```tsx
const columns: DataTableColumnDef<Person>[] = [
  {
    accessorKey: "name",
    header: "Name",
    meta: {
      renderCell: ({ defaultProps, content }) => (
        <td {...defaultProps} style={{ fontWeight: 600, ...defaultProps.style }}>
          {content}
        </td>
      ),
    },
  },
  // ...
]
```

**Custom render props (bordered table + custom empty):**
```tsx
const renderers: DataTableRenderers<Person> = {
  renderTable: ({ defaultProps, children }) => (
    <table {...defaultProps} style={{ borderCollapse: "collapse", width: "100%" }}>
      {children}
    </table>
  ),
  renderHeaderCell: ({ defaultProps, content }) => (
    <th {...defaultProps} style={{ borderBottom: "1px solid #e5e7eb", padding: "8px 10px" }}>
      {content}
    </th>
  ),
  renderEmptyState: ({ colSpan }) => (
    <tr>
      <td colSpan={colSpan} style={{ textAlign: "center", padding: 12 }}>
        Nothing to show yet.
      </td>
    </tr>
  ),
}

<DataTable columns={columns} data={[]} renderers={renderers} />
```

**Sortable header via meta:**
```tsx
{
  accessorKey: "name",
  header: "Name",
  meta: {
    renderHeaderCell: ({ header, defaultProps, content }) => (
      <th {...defaultProps} onClick={header.column.getToggleSortingHandler()}>
        {content}
      </th>
    ),
  },
}
```

### Notes
- Sticky header/footer rely on the scroll container being the immediate ancestor.
- Grouped headers are supported; `colSpan`/`rowSpan` are applied so category headings align with their leaf columns.
- Column filter wiring assumes a `description` column when `filterValue` is passed (kept for backward compatibility).
- Render order: column meta renderers run first; if they return `undefined`, table-level renderers handle fallback.

### For LLMs / maintainers
- DataTable is a thin headless layer over TanStack Table v8 with shadcn defaults. Prefer extending via render props and `column.meta` rather than editing core rendering.
- Structural defaults (sticky header/footer, scroll area) are intentional; avoid removing unless updating consumers.
- `filterValue` targets column id `description`. Preserve unless coordinating downstream changes.
- Keys are provided in `defaultProps`; preserve them when wrapping elements.
- Keep stories in sync with API changes—they are primary usage docs (see `DataTable.stories.tsx`). 
Composable TanStack-powered table with Shadcn styling, optional sorting/filtering, and hooks for custom row/column rendering (used by InlineTable/ExpandableTable).

## Features
- Styled shell (sticky header/footer, scrollable body) using Shadcn `Table` primitives.
- Opt-in sorting/filtering; can be fully disabled for static layouts.
- Column metadata for alignment, header/cell class/style, row-header semantics, and per-cell/header render overrides.
- Row-level hooks for custom classNames and render overrides.
- Optional expansion support via `getSubRows`, with depth-aware padding and toggle callbacks for custom expand icons.
- Column visibility helpers to hide empty columns while preserving row-header columns.

## Quick Start
```tsx
import { DataTable, type DataTableColumnDef } from "@/registry/v2/components/DataTable/DataTable"

type Row = { id: string; name: string; status: string; spend: number }

const columns: DataTableColumnDef<Row, unknown>[] = [
  { id: "name", header: "Name", accessorKey: "name", meta: { isRowHeader: true } },
  { id: "status", header: "Status", accessorKey: "status" },
  {
    id: "spend",
    header: "Spend",
    accessorKey: "spend",
    cell: ({ getValue }) => `$${getValue<number>().toLocaleString()}`,
    meta: { align: "right", cellClassName: "tabular-nums" },
  },
]

const data: Row[] = [
  { id: "a1", name: "Acme", status: "Active", spend: 128000 },
  { id: "b1", name: "Bravo", status: "Paused", spend: 42000 },
]

<DataTable
  className="h-[360px]"
  columns={columns}
  data={data}
  enableSorting={false}
  enableFiltering={false}
  hideFooter
/>;
```

## Column Meta (per column)
- `align?: "left" | "right" | "center"`
- `className?`, `headerClassName?`, `cellClassName?`
- `headerStyle?`, `cellStyle?`
- `isRowHeader?: boolean` (left-aligns, participates in visibility filtering)
- `renderHeader?(args)` and `renderCell?(args)` to override rendering while keeping default classes/styles.

`renderCell` receives expansion context when `getSubRows` is provided:
`{ value, defaultClassName, defaultStyle, depth, isExpanded, isExpandable, toggleExpanded }`.

## Row/Visibility Hooks
- `getRowClassName?(row)` to add variant/striping/totals classes.
- `renderRow?(args)` to replace the default `<TableRow>`.
- `forceShowAllColumns?: boolean` or `computeVisibleColumns?(cols, data)` to mirror Inline/Expandable behavior of hiding empty columns.

## Expansion (optional)
Provide `getSubRows={(row) => row.children}` and (optionally) `expanded`, `defaultExpanded`, `onExpandedChange`, `indentSizeRem`. Use `renderCell` on your row-header column to show chevrons via `args.isExpandable`/`args.isExpanded` and `args.toggleExpanded`.

## Props snapshot
```ts
type DataTableProps<TData, TValue> = {
  columns: DataTableColumnDef<TData, TValue>[]
  data: TData[]
  className?: string
  tableClassName?: string
  headerClassName?: string
  bodyClassName?: string
  footerClassName?: string
  wrapperClassName?: string
  hideFooter?: boolean
  enableSorting?: boolean
  enableFiltering?: boolean
  forceShowAllColumns?: boolean
  computeVisibleColumns?: (cols, data) => typeof cols
  getRowClassName?: (row) => string | undefined
  renderRow?: (args) => React.ReactNode
  renderHeader?: (args) => React.ReactNode
  renderCell?: (args) => React.ReactNode
  getRowId?: (row, index, parent?) => string
  getSubRows?: (row) => TData[] | undefined
  expanded?: ExpandedState
  defaultExpanded?: ExpandedState
  onExpandedChange?: (state) => void
  indentSizeRem?: number
}
```
