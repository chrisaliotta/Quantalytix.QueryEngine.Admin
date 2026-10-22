import type { ColumnDef } from '@tanstack/react-table';
import { ContentTab } from '@/shared/ContentTab';
import { useDeleteReportStyle, useReportStyles } from './api';
import { styleCss } from './styleForm';
import { StyleSheet } from './StyleSheet';
import type { ReportStyle } from './types';

const columns: ColumnDef<ReportStyle>[] = [
  { accessorKey: 'styleKey', header: 'Key' },
  {
    accessorKey: 'displayName',
    header: 'Name',
    cell: ({ row }) => (
      <span className="inline-block rounded-sm px-2 py-0.5" style={styleCss(row.original)}>
        {row.original.displayName}
      </span>
    ),
  },
  { id: 'defaultRole', header: 'Default for', accessorFn: style => style.defaultRole ?? '' },
];

/** The report's styles: how the rows that name each show, and the rows of each role that name none. */
export function StylesTab({ reportId }: { reportId: number }) {
  const styles = useReportStyles(reportId);
  const deleteStyle = useDeleteReportStyle(reportId);

  return (
    <ContentTab
      rows={styles}
      columns={columns}
      noun="style"
      keyOf={style => style.styleKey}
      deleteDescription="A style a row of the report shows is kept; pick another style for the row first."
      deleteRow={{ mutate: style => deleteStyle.mutate(style.styleKey), error: deleteStyle.error }}
      renderSheet={(style, onClose) => <StyleSheet reportId={reportId} style={style} onClose={onClose} />}
    />
  );
}
