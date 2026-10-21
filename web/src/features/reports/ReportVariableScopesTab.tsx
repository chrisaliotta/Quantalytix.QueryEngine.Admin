import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useReplaceReportVariableScopes, useReportVariableScopes } from './api';
import { ReportVariableScopeSheet } from './ReportVariableScopeSheet';
import type { ReportVariableScopeInput, ReportVersion } from './types';

export interface ReportVariableScopesTabProps {
  version: ReportVersion;
  isEditable: boolean;
}

/** What the scope sheet was opened for: an existing scope, or a new one. */
interface ScopeSheetRequest {
  scope: ReportVariableScopeInput | null;
}

/**
 * The version's variable scopes: the member whose branch each scoped variable reads. A draft or the published version adds,
 * edits and deletes scopes one at a time, each saved as a replace of the whole list.
 */
export function ReportVariableScopesTab({ version, isEditable }: ReportVariableScopesTabProps) {
  const scopes = useReportVariableScopes(version.reportVersionId);
  const save = useReplaceReportVariableScopes(version.reportVersionId);
  const remove = useReplaceReportVariableScopes(version.reportVersionId);
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
      cell: ({ row }) =>
        isEditable && (
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
        A scoped variable reads only its member's branch of the report: rows inside it read their own value, rows above it read
        the member's value, and other rows read nothing. A variable without a scope reads every row its sources classify.
      </p>
      <EntityTable
        columns={columns}
        data={scopes.data ? saved : undefined}
        isLoading={scopes.isPending}
        error={scopes.error}
        searchPlaceholder="Search scopes..."
        emptyMessage="No scopes yet; every variable reads every row its sources classify."
        onRowClick={scope => setSheet({ scope })}
        toolbar={
          isEditable && (
            <Button size="sm" className="ml-auto" onClick={() => setSheet({ scope: null })}>
              <Plus />
              New scope
            </Button>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {sheet && (
        <ReportVariableScopeSheet
          version={version}
          scope={sheet.scope}
          readOnly={!isEditable}
          onSave={scope => save.mutateAsync([...without(sheet.scope?.variableKey), scope])}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the scope of ${deleting?.variableKey ?? ''}?`}
        description="The variable then reads every row its sources classify."
        confirmLabel="Delete scope"
        destructive
        onConfirm={() => deleting && remove.mutate(without(deleting.variableKey))}
      />
    </div>
  );
}
