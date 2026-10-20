import type { ColumnDef } from '@tanstack/react-table';
import { Link } from 'react-router';
import { EntityTable } from '@/shared/EntityTable';
import { StatusBadge } from '@/shared/StatusBadge';
import { useTenantId } from '@/shared/useTenantId';
import { pinKindLabel, pinLink } from './pinLinks';
import type { AssumptionPin } from './types';

/** A pin as the table shows it: its kind label beside the pin. */
interface PinRow extends AssumptionPin {
  kindLabel: string;
}

/**
 * An assumption set's pins: for each dimension, mapping set, bridge, calculation model and report, the version the set
 * pins, linking to it, with its status. A historical set's pins are retired once later versions are published.
 */
export function AssumptionPinsTable({ pins }: { pins: AssumptionPin[] }) {
  const tenantId = useTenantId();
  const columns: ColumnDef<PinRow>[] = [
    { accessorKey: 'kindLabel', header: 'Kind' },
    { accessorKey: 'identityKey', header: 'Identity' },
    {
      id: 'version',
      header: 'Version',
      cell: ({ row }) => (
        <Link className="font-medium hover:underline" to={pinLink(tenantId, row.original)}>
          v{row.original.versionNumber}
        </Link>
      ),
    },
    { accessorKey: 'status', header: 'Status', cell: ({ row }) => <StatusBadge status={row.original.status} /> },
  ];
  return (
    <EntityTable
      columns={columns}
      data={pins.map(pin => ({ ...pin, kindLabel: pinKindLabel(pin) }))}
      isLoading={false}
      error={null}
      searchPlaceholder="Search pins..."
      emptyMessage="No pins yet. Publishing a version pins it in the active set."
    />
  );
}
