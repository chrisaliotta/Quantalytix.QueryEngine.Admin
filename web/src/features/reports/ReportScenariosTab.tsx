import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityTable } from '@/shared/EntityTable';
import { useLookup } from '@/shared/useLookup';
import { useReplaceReportScenarios, useReportScenarios } from './api';
import { ReportScenarioSheet } from './ReportScenarioSheet';
import type { ReportScenario, ReportScenarioInput } from './types';

/**
 * The report's scenarios: the dimensions whose members a run chooses one of, each with the key requests name it by, which is its
 * dimension's one role's key. Scenarios are added and deleted one at a time, each saved as a replace of the whole list.
 */
export function ReportScenariosTab({ reportId }: { reportId: number }) {
  const scenarios = useReportScenarios(reportId);
  const save = useReplaceReportScenarios(reportId);
  const remove = useReplaceReportScenarios(reportId);
  const dimensions = useLookup('dimensions');
  const [adding, setAdding] = useState(false);
  const [deleting, setDeleting] = useState<ReportScenario | null>(null);
  const dimensionNames = new Map(dimensions.data?.map(item => [item.id, item.label]));
  const saved: ReportScenarioInput[] = (scenarios.data ?? []).map(({ dimensionRoleId }) => ({ dimensionRoleId }));

  function without(dimensionRoleId: number) {
    return saved.filter(other => other.dimensionRoleId !== dimensionRoleId);
  }

  function dimensionName(scenario: ReportScenario) {
    return dimensionNames.get(scenario.dimensionId) ?? scenario.dimensionRoleKey;
  }

  const columns: ColumnDef<ReportScenario>[] = [
    { id: 'dimension', header: 'Dimension', accessorFn: dimensionName },
    {
      accessorKey: 'dimensionRoleKey',
      header: 'Request key',
      cell: ({ row }) => <span className="font-mono text-xs">{row.original.dimensionRoleKey}</span>,
    },
    {
      id: 'actions',
      header: '',
      cell: ({ row }) => (
        <Button
          variant="ghost"
          size="icon-xs"
          aria-label={`Delete the scenario ${dimensionName(row.original)}`}
          onClick={() => setDeleting(row.original)}
        >
          <Trash2 />
        </Button>
      ),
    },
  ];

  return (
    <div data-slot="report-scenarios-tab" className="space-y-3">
      <p className="text-sm text-muted-foreground">
        A scenario is a dimension whose members a run chooses one of, such as a plan version or a rate shock. A run or comparison
        names one member of each scenario its sources read, under the scenario's request key; sources that do not read it are
        unaffected.
      </p>
      <EntityTable
        columns={columns}
        data={scenarios.data}
        isLoading={scenarios.isPending}
        error={scenarios.error}
        searchPlaceholder="Search scenarios..."
        emptyMessage="No scenarios yet."
        toolbar={
          <Button size="sm" className="ml-auto" onClick={() => setAdding(true)}>
            <Plus />
            New scenario
          </Button>
        }
      />
      <ApiErrorAlert error={remove.error} />
      {adding && (
        <ReportScenarioSheet
          reportId={reportId}
          scenarios={scenarios.data ?? []}
          onSave={dimensionRoleId => save.mutateAsync([...saved, { dimensionRoleId }])}
          onClose={() => setAdding(false)}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={open => !open && setDeleting(null)}
        title={`Delete the scenario ${deleting ? dimensionName(deleting) : ''}?`}
        description="Runs then name no member of the dimension, so the sources whose data carries it read all of its members together."
        confirmLabel="Delete scenario"
        destructive
        onConfirm={() => deleting && remove.mutate(without(deleting.dimensionRoleId))}
      />
    </div>
  );
}
