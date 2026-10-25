import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Button } from '@/components/ui/button';
import { bridgePath } from '@/features/dimensions/paths';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { EntityTable } from '@/shared/EntityTable';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { useBridges } from './api';
import { NewBridgeSheet } from './NewBridgeSheet';
import type { Bridge, BridgeSummary } from './types';

/** A bridge with the dimension at its other end, which the table shows and searches, and the page it opens. */
interface BridgeRow extends Bridge {
  otherEnd: string;
  path: string;
}

const enabledColumn: ColumnDef<BridgeRow> = {
  accessorKey: 'isEnabled',
  header: 'Enabled',
  cell: ({ row }) => <EnabledBadge enabled={row.original.isEnabled} />,
};

const intoColumns: ColumnDef<BridgeRow>[] = [
  { accessorKey: 'bridgeKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'otherEnd', header: 'From' },
  enabledColumn,
];

const fromColumns: ColumnDef<BridgeRow>[] = [
  { accessorKey: 'bridgeKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'otherEnd', header: 'Into' },
  enabledColumn,
];

export interface BridgesTabProps {
  dimensionId: number;
  dimensionKey: string;
}

/**
 * A dimension's bridges. The bridges into it are its own: they are created here and each opens on its own page. The bridges that
 * start from it belong to their targets, so they list read-only with a link to where each one changes: an analytical bridge on its
 * target's page, a report bridge in its report's editor. Editing this dimension's members changes what all of them read.
 */
export function BridgesTab({ dimensionId, dimensionKey }: BridgesTabProps) {
  const tenantId = useTenantId();
  const bridges = useBridges(dimensionId);
  const dimensions = useLookup('dimensions');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const dimensionKeys = new Map((dimensions.data ?? []).map(dimension => [dimension.id, dimension.key]));
  const summaries = bridges.data ?? [];

  function row(summary: BridgeSummary, otherEnd: string): BridgeRow {
    const { bridge, owner } = summary;
    return {
      ...bridge,
      otherEnd,
      path: owner
        ? `/t/${tenantId}/reports/${owner.reportId}?source=${owner.reportSourceId}`
        : bridgePath(tenantId, bridge.targetDimensionId, bridge.bridgeId),
    };
  }

  const into = summaries
    .filter(summary => summary.bridge.targetDimensionId === dimensionId)
    .map(summary => row(summary, dimensionKeys.get(summary.bridge.sourceDimensionId) ?? ''));
  const from = summaries
    .filter(summary => summary.bridge.sourceDimensionId === dimensionId)
    .map(summary =>
      row(
        summary,
        summary.owner
          ? `Report ${summary.owner.reportKey} · ${summary.owner.sourceKey}`
          : (dimensionKeys.get(summary.bridge.targetDimensionId) ?? '')
      )
    );
  const isLoading = bridges.isPending || dimensions.isPending;

  return (
    <div data-slot="bridges-tab" className="space-y-6">
      <section className="space-y-3">
        <h3 className="text-sm font-medium">Into {dimensionKey}</h3>
        <p className="text-sm text-muted-foreground">
          Each bridge remaps another dimension's members onto this one's, with factors and allocation weights, so a dataset
          classified by the other dimension can be read by this one. Its versions are its own.
        </p>
        <EntityTable
          columns={intoColumns}
          data={isLoading ? undefined : into}
          isLoading={isLoading}
          error={bridges.error ?? dimensions.error}
          emptyMessage="No bridges lead into this dimension."
          onRowClick={bridge => navigate(bridge.path)}
          toolbar={
            <Button size="sm" className="ml-auto" onClick={() => setCreating(true)}>
              <Plus />
              New bridge
            </Button>
          }
        />
      </section>
      <section className="space-y-3">
        <h3 className="text-sm font-medium">From {dimensionKey}</h3>
        <p className="text-sm text-muted-foreground">
          Bridges that read this dimension's members, including the bridges of report sources. They change on their target's page
          or in their report's editor, but moving or removing a member here changes what they read.
        </p>
        <EntityTable
          columns={fromColumns}
          data={isLoading ? undefined : from}
          isLoading={isLoading}
          emptyMessage="No bridges start from this dimension."
          onRowClick={bridge => navigate(bridge.path)}
        />
      </section>
      {creating && (
        <NewBridgeSheet
          targetDimensionId={dimensionId}
          onClose={() => setCreating(false)}
          onCreated={bridge => navigate(bridgePath(tenantId, dimensionId, bridge.bridgeId))}
        />
      )}
    </div>
  );
}
