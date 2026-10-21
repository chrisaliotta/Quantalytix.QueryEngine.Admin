import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useReplaceReportVariableScopes, useReportVariableScopes } from './api';
import { ReportVariableScopeSheet } from './ReportVariableScopeSheet';
import type { EditorView, ReportVariableScopeInput } from './types';

/** What the scope sheet was opened for: an existing scope, or a new one. */
interface ScopeSheetRequest {
  scope: ReportVariableScopeInput | null;
}

/**
 * The report's variable scopes: the row whose branch each scoped variable reads. Scopes are added, edited and deleted one at a
 * time, each saved as a replace of the whole list.
 */
export function ReportVariableScopesTab({ view }: { view: EditorView }) {
  const reportId = view.report.reportId;
  const scopes = useReportVariableScopes(reportId);
  const save = useReplaceReportVariableScopes(reportId);
  const remove = useReplaceReportVariableScopes(reportId);
  const [sheet, setSheet] = useState<ScopeSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<ReportVariableScopeInput | null>(null);
  const saved: ReportVariableScopeInput[] = (scopes.data ?? []).map(({ variableKey, scopeMemberKey, selectionScope }) => ({
    variableKey,
    scopeMemberKey,
    selectionScope,
  }));

  function without(variableKey: string | undefined) {
    return saved.filter(other => other.variableKey !== variableKey);
  }

  const columns: ColumnDef<ReportVariableScopeInput>[] = [
    { accessorKey: 'variableKey', header: 'Variable' },
    { accessorKey: 'scopeMemberKey', header: 'Row' },
    { accessorKey: 'selectionScope', header: 'Selection' },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={`Delete the scope of ${row.original.variableKey}`}
          onClick={event => {
            event.stopPropagation();
            setDeleting(row.original);
          }}
        >
          <Trash2 />
        </Button>
      ),
    },
  ];

  return (
    <div data-slot="report-variable-scopes-tab" className="space-y-3">
      <p className="text-sm text-muted-foreground">
        A scoped variable reads only its row's branch of the report: rows inside it read their own value, rows above it read the
        row's value, and other rows read nothing. A variable without a scope reads every row its sources reach.
      </p>
      <EntityTable
        columns={columns}
        data={scopes.data ? saved : undefined}
        isLoading={scopes.isPending}
        error={scopes.error}
        searchPlaceholder="Search scopes..."
        emptyMessage="No scopes yet; every variable reads every row its sources reach."
        onRowClick={scope => setSheet({ scope })}
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setSheet({ scope: null })}>
            <Plus />
            New scope
          </Button>
        }
      />
      <ApiErrorAlert error={remove.error} />
      {sheet && (
        <ReportVariableScopeSheet
          view={view}
          scope={sheet.scope}
          onSave={scope => save.mutateAsync([...without(sheet.scope?.variableKey), scope])}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the scope of ${deleting?.variableKey ?? ''}?`}
        description="The variable then reads every row its sources reach."
        confirmLabel="Delete scope"
        destructive
        onConfirm={() => deleting && remove.mutate(without(deleting.variableKey))}
      />
    </div>
  );
}
