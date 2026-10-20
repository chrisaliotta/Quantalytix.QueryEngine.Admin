import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useReplaceReportVariableTargets, useReportVariableTargets } from './api';
import { ReportVariableTargetSheet } from './ReportVariableTargetSheet';
import type { Report, ReportVariableTargetInput, ReportVersion } from './types';

export interface ReportVariableTargetsTabProps {
  report: Report;
  version: ReportVersion;
  isEditable: boolean;
}

/** What the target sheet was opened for: an existing target, or a new one. */
interface TargetSheetRequest {
  target: ReportVariableTargetInput | null;
}

/**
 * The version's variable targets: the member each targeted variable reads at report scope. A draft or the published version
 * adds, edits and deletes targets one at a time, each saved as a replace of the whole list.
 */
export function ReportVariableTargetsTab({ report, version, isEditable }: ReportVariableTargetsTabProps) {
  const targets = useReportVariableTargets(version.reportVersionId);
  const save = useReplaceReportVariableTargets(version.reportVersionId);
  const remove = useReplaceReportVariableTargets(version.reportVersionId);
  const [sheet, setSheet] = useState<TargetSheetRequest | null>(null);
  const [deleting, setDeleting] = useState<ReportVariableTargetInput | null>(null);
  const saved: ReportVariableTargetInput[] = (targets.data ?? []).map(({ variableKey, targetMemberKey, selectionScope }) => ({
    variableKey,
    targetMemberKey,
    selectionScope,
  }));

  function without(variableKey: string | undefined) {
    return saved.filter(other => other.variableKey !== variableKey);
  }

  const columns: ColumnDef<ReportVariableTargetInput>[] = [
    { accessorKey: 'variableKey', header: 'Variable' },
    { accessorKey: 'targetMemberKey', header: 'Target member' },
    { accessorKey: 'selectionScope', header: 'Scope' },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) =>
        isEditable && (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Delete the target of ${row.original.variableKey}`}
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
    <div data-slot="report-variable-targets-tab" className="space-y-3">
      <EntityTable
        columns={columns}
        data={targets.data ? saved : undefined}
        isLoading={targets.isPending}
        error={targets.error}
        searchPlaceholder="Search targets..."
        emptyMessage="No targets yet; every variable reads every row its sources classify."
        onRowClick={target => setSheet({ target })}
        toolbar={
          isEditable && (
            <Button size="sm" className="ml-auto" onClick={() => setSheet({ target: null })}>
              <Plus />
              New target
            </Button>
          )
        }
      />
      <ApiErrorAlert error={remove.error} />
      {sheet && (
        <ReportVariableTargetSheet
          report={report}
          version={version}
          target={sheet.target}
          readOnly={!isEditable}
          onSave={target => save.mutateAsync([...without(sheet.target?.variableKey), target])}
          onClose={() => setSheet(null)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the target of ${deleting?.variableKey ?? ''}?`}
        description="The variable then reads every row its sources classify."
        confirmLabel="Delete target"
        destructive
        onConfirm={() => deleting && remove.mutate(without(deleting.variableKey))}
      />
    </div>
  );
}
