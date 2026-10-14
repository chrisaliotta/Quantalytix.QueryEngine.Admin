"use client"

import * as React from "react"
import {
  type Cell,
  type ColumnDef,
  type ColumnFiltersState,
  type Header,
  type HeaderGroup,
  type Row,
  type SortingState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table"

import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"

import { cn } from "@/lib/utils"

type DivProps = React.HTMLAttributes<HTMLDivElement>
type TableProps = React.TableHTMLAttributes<HTMLTableElement>
type THeadProps = React.HTMLAttributes<HTMLTableSectionElement>
type TBodyProps = React.HTMLAttributes<HTMLTableSectionElement>
type TFootProps = React.HTMLAttributes<HTMLTableSectionElement>
type TrProps = React.HTMLAttributes<HTMLTableRowElement>
type ThProps = React.ThHTMLAttributes<HTMLTableCellElement>
type TdProps = React.TdHTMLAttributes<HTMLTableCellElement>
type WithKey<T> = T & { key?: React.Key }

/**
 * Column-level overrides for table cells and headers.
 */
export type DataTableColumnMeta<TData> = {
  renderHeaderCell?: (args: DataTableHeaderCellRenderArgs<TData>) => React.ReactNode
  renderCell?: (args: DataTableCellRenderArgs<TData>) => React.ReactNode
}

export type DataTableColumnDef<TData> = ColumnDef<TData> & {
  meta?: DataTableColumnMeta<TData>
}

export type DataTableHeaderCellRenderArgs<TData> = {
  header: Header<TData, unknown>
  defaultProps: WithKey<ThProps>
  content: React.ReactNode
}

export type DataTableCellRenderArgs<TData> = {
  cell: Cell<TData, unknown>
  defaultProps: WithKey<TdProps>
  content: React.ReactNode
}

export type DataTableRowRenderArgs<TData> = {
  row: Row<TData>
  defaultProps: WithKey<TrProps>
  cells: React.ReactNode[]
}

export type DataTableHeaderRowRenderArgs<TData> = {
  headerGroup: HeaderGroup<TData>
  defaultProps: WithKey<TrProps>
  cells: React.ReactNode[]
}

export type DataTableFooterCellRenderArgs<TData> = {
  header: Header<TData, unknown>
  defaultProps: WithKey<TdProps>
  content: React.ReactNode
}

export type DataTableFooterRowRenderArgs<TData> = {
  footerGroup: HeaderGroup<TData>
  defaultProps: WithKey<TrProps>
  cells: React.ReactNode[]
}

export type DataTableRenderers<TData> = {
  renderContainer?: (args: { defaultProps: DivProps; children: React.ReactNode }) => React.ReactNode
  renderScrollArea?: (args: { defaultProps: DivProps; children: React.ReactNode }) => React.ReactNode
  renderTable?: (args: { defaultProps: TableProps; children: React.ReactNode }) => React.ReactNode
  renderHeader?: (args: { defaultProps: THeadProps; children: React.ReactNode }) => React.ReactNode
  renderHeaderRow?: (args: DataTableHeaderRowRenderArgs<TData>) => React.ReactNode
  renderHeaderCell?: (args: DataTableHeaderCellRenderArgs<TData>) => React.ReactNode
  renderBody?: (args: { defaultProps: TBodyProps; children: React.ReactNode }) => React.ReactNode
  renderRow?: (args: DataTableRowRenderArgs<TData>) => React.ReactNode
  renderCell?: (args: DataTableCellRenderArgs<TData>) => React.ReactNode
  renderEmptyState?: (args: { defaultCellProps: WithKey<TdProps>; colSpan: number }) => React.ReactNode
  renderFooter?: (args: { defaultProps: TFootProps; children: React.ReactNode }) => React.ReactNode
  renderFooterRow?: (args: DataTableFooterRowRenderArgs<TData>) => React.ReactNode
  renderFooterCell?: (args: DataTableFooterCellRenderArgs<TData>) => React.ReactNode
}

interface DataTableProps<TData> {
  /**
   * TanStack columns. Use `meta.renderHeaderCell` / `meta.renderCell` to apply
   * per-column wrappers or styling while leaving the table headless by default.
   */
  columns: DataTableColumnDef<TData>[]
  /**
   * Table rows.
   */
  data: TData[]
  /**
   * Optional filter value applied to the `description` column.
   */
  filterValue?: string
  /**
   * Appended to the container element.
   */
  className?: string
  /**
   * Headless render hooks for every table surface. Each receives minimal
   * default props so consumers can extend rather than replace.
   */
  renderers?: DataTableRenderers<TData>
}

/**
 * Headless DataTable built on TanStack Table.
 *
 * Defaults keep only structural layout (scroll area + sticky header/footer)
 * without theme styling. All surface areas can be overridden with render props,
 * plus per-column renderers via `column.meta`.
 */
export function DataTable<TData>({
  columns,
  data,
  filterValue,
  className,
  renderers,
}: DataTableProps<TData>) {
  const [sorting, setSorting] = React.useState<SortingState>([])
  const [columnFilters, setColumnFilters] = React.useState<ColumnFiltersState>([])

  React.useEffect(() => {
    if (filterValue !== undefined) {
      setColumnFilters([{ id: "description", value: filterValue }])
    }
  }, [filterValue])

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    state: {
      sorting,
      columnFilters,
    },
  })

  const containerProps: DivProps = {
    className: cn("h-full flex flex-col", className),
  }

  const scrollAreaProps: DivProps = {
    className: "relative flex-1 overflow-auto",
  }

  const tableProps: TableProps = {
    className: "w-full",
    style: { tableLayout: "auto", borderCollapse: "collapse" },
  }

  const headerProps: THeadProps = {
    style: { position: "sticky", top: 0, zIndex: 1 },
  }

  const footerProps: TFootProps = {
    style: { position: "sticky", bottom: 0, zIndex: 1 },
  }

  const bodyProps: TBodyProps = {}

  const renderContainer =
    renderers?.renderContainer ??
    (({ defaultProps, children }) => <div {...defaultProps}>{children}</div>)

  const renderScrollArea =
    renderers?.renderScrollArea ??
    (({ defaultProps, children }) => <div {...defaultProps}>{children}</div>)

  const renderTable =
    renderers?.renderTable ?? (({ defaultProps, children }) => <Table {...defaultProps}>{children}</Table>)

  const renderHeader =
    renderers?.renderHeader ?? (({ defaultProps, children }) => <TableHeader {...defaultProps}>{children}</TableHeader>)

  const renderHeaderRow =
    renderers?.renderHeaderRow ??
    (({ defaultProps, cells }) => {
      const { key, ...restProps } = defaultProps
      return <TableRow key={key} {...restProps}>{cells}</TableRow>
    })

  const renderHeaderCell =
    renderers?.renderHeaderCell ??
    (({ defaultProps, content }) => {
      const { key, ...restProps } = defaultProps
      return <TableHead key={key} {...restProps}>{content}</TableHead>
    })

  const renderBody =
    renderers?.renderBody ?? (({ defaultProps, children }) => <TableBody {...defaultProps}>{children}</TableBody>)

  const renderRow =
    renderers?.renderRow ??
    (({ defaultProps, cells }) => {
      const { key, ...restProps } = defaultProps
      return <TableRow key={key} {...restProps}>{cells}</TableRow>
    })

  const renderCell =
    renderers?.renderCell ??
    (({ defaultProps, content }) => {
      const { key, ...restProps } = defaultProps
      return <TableCell key={key} {...restProps}>{content}</TableCell>
    })

  const renderEmptyState =
    renderers?.renderEmptyState ??
    (({ defaultCellProps, colSpan }) => (
      <TableRow>
        <TableCell {...defaultCellProps} colSpan={colSpan} style={{ textAlign: "center", padding: "0.75rem" }}>
          No results.
        </TableCell>
      </TableRow>
    ))

  const renderFooter =
    renderers?.renderFooter ?? (({ defaultProps, children }) => <TableFooter {...defaultProps}>{children}</TableFooter>)

  const renderFooterRow =
    renderers?.renderFooterRow ??
    (({ defaultProps, cells }) => {
      const { key, ...restProps } = defaultProps
      return <TableRow key={key} {...restProps}>{cells}</TableRow>
    })

  const renderFooterCell =
    renderers?.renderFooterCell ??
    (({ defaultProps, content }) => {
      const { key, ...restProps } = defaultProps
      return <TableCell key={key} {...restProps}>{content}</TableCell>
    })

  const renderHeaderCellWithMeta = (args: DataTableHeaderCellRenderArgs<TData>) => {
    const columnMeta = args.header.column.columnDef.meta as DataTableColumnMeta<TData> | undefined
    const byColumn = columnMeta?.renderHeaderCell?.(args)
    if (byColumn !== undefined) return byColumn
    const byTable = renderHeaderCell(args)
    return byTable
  }

  const renderCellWithMeta = (args: DataTableCellRenderArgs<TData>) => {
    const columnMeta = args.cell.column.columnDef.meta as DataTableColumnMeta<TData> | undefined
    const byColumn = columnMeta?.renderCell?.(args)
    if (byColumn !== undefined) return byColumn
    return renderCell(args)
  }

  return renderContainer({
    defaultProps: containerProps,
    children: renderScrollArea({
      defaultProps: scrollAreaProps,
      children: renderTable({
        defaultProps: tableProps,
        children: (
          <>
            {renderHeader({
              defaultProps: headerProps,
              children: table.getHeaderGroups().map((headerGroup) => {
                const headerCells = headerGroup.headers.map((header) => {
                  const defaultProps: WithKey<ThProps> = {
                    key: header.id,
                    style: {
                      position: "sticky",
                      top: 0,
                      zIndex: 2,
                      background: "inherit",
                    },
                  }

                  const content = header.isPlaceholder
                    ? null
                    : flexRender(header.column.columnDef.header, header.getContext())

                  return renderHeaderCellWithMeta({
                    header,
                    defaultProps,
                    content,
                  })
                })

                return renderHeaderRow({
                  headerGroup,
                  defaultProps: { key: headerGroup.id },
                  cells: headerCells,
                })
              }),
            })}

            {renderBody({
              defaultProps: bodyProps,
              children:
                table.getRowModel().rows?.length > 0
                  ? table.getRowModel().rows.map((row) => {
                    const cells = row.getVisibleCells().map((cell) => {
                      const defaultProps: WithKey<TdProps> = { key: cell.id }
                      const content = flexRender(cell.column.columnDef.cell, cell.getContext())
                      return renderCellWithMeta({
                        cell,
                        defaultProps,
                        content,
                      })
                    })

                    return renderRow({
                      row,
                      defaultProps: { key: row.id },
                      cells,
                    })
                  })
                  : renderEmptyState({
                    defaultCellProps: {},
                    colSpan: columns.length,
                  }),
            })}

            {table.getFooterGroups().length > 0 &&
              renderFooter({
                defaultProps: footerProps,
                children: table.getFooterGroups().map((group) => {
                  const footerCells = group.headers.map((header) => {
                    const defaultProps: WithKey<TdProps> = { key: header.id }

                    const content = header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.footer, header.getContext())

                    return renderFooterCell({
                      header,
                      defaultProps,
                      content,
                    })
                  })

                  return renderFooterRow({
                    footerGroup: group,
                    defaultProps: { key: group.id },
                    cells: footerCells,
                  })
                }),
              })}
          </>
        ),
      }),
    }),
  })
}
