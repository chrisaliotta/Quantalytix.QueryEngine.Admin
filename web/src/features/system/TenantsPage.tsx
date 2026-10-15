import type { ColumnDef } from '@tanstack/react-table';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { TruncatedText } from '@registry/components/TruncatedText';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EntityListPage } from '@/shared/EntityListPage';
import { useSheetForm } from '@/shared/useSheetForm';
import { formatDateTime } from '@/shared/utcTime';
import { EditTenantSheet } from './EditTenantSheet';
import { NewTenantSheet } from './NewTenantSheet';
import type { TenantCreateInput, TenantDirectoryEntry, TenantEditValues } from './types';
import { useSetTenantEnabled, useTenantDirectory } from './useSystemTenants';

/** A new tenant's starting values: the only provider, enabled, and the directory's default cache lifetime. */
const NEW_TENANT: TenantCreateInput = {
  tenantId: '',
  providerId: 'starrocks',
  connectionString: '',
  isEnabled: true,
  metadataCacheSeconds: 60,
};

/**
 * The control plane's tenant directory (SystemAdmin only): connection strings are masked, a row
 * opens its edit sheet, and the switch enables or, after a confirmation, disables query routing.
 */
export function TenantsPage() {
  const tenants = useTenantDirectory();
  const setEnabled = useSetTenantEnabled();
  const newTenant = useSheetForm<TenantCreateInput>(NEW_TENANT);
  const editTenant = useSheetForm<TenantEditValues>({ tenantId: '', providerId: '', connectionString: '', metadataCacheSeconds: 0 });
  const [disabling, setDisabling] = useState<TenantDirectoryEntry | null>(null);

  function changeEnabled(entry: TenantDirectoryEntry, isEnabled: boolean) {
    if (isEnabled) {
      setEnabled.mutate({ tenantId: entry.tenantId, isEnabled });
      return;
    }
    setDisabling(entry);
  }

  const columns: ColumnDef<TenantDirectoryEntry>[] = [
    { accessorKey: 'tenantId', header: 'Tenant ID' },
    { accessorKey: 'providerId', header: 'Provider' },
    {
      accessorKey: 'connectionString',
      header: 'Connection string',
      cell: ({ row }) => (
        <span className="block max-w-56 font-mono text-xs">
          <TruncatedText text={row.original.connectionString} />
        </span>
      ),
    },
    {
      accessorKey: 'isEnabled',
      header: 'Enabled',
      cell: ({ row }) => (
        <Switch
          checked={row.original.isEnabled}
          disabled={setEnabled.isPending}
          aria-label={`Route queries to ${row.original.tenantId}`}
          onClick={event => event.stopPropagation()}
          onCheckedChange={checked => changeEnabled(row.original, checked)}
        />
      ),
    },
    { accessorKey: 'metadataCacheSeconds', header: 'Cache (s)' },
    { accessorKey: 'updatedAt', header: 'Updated', cell: ({ row }) => formatDateTime(row.original.updatedAt) },
  ];

  return (
    <>
      <EntityListPage
        title="Tenants"
        description="The control plane's tenant directory routes each tenant to its metadata database."
        actions={
          <Button onClick={() => newTenant.open(NEW_TENANT)}>
            <Plus />
            New tenant
          </Button>
        }
        columns={columns}
        data={tenants.data}
        isLoading={tenants.isPending}
        error={tenants.error ?? setEnabled.error}
        searchPlaceholder="Search tenants..."
        onRowClick={entry =>
          editTenant.open({
            tenantId: entry.tenantId,
            providerId: entry.providerId,
            connectionString: '',
            metadataCacheSeconds: entry.metadataCacheSeconds,
          })
        }
      />
      <NewTenantSheet {...newTenant.sheetProps} />
      <EditTenantSheet {...editTenant.sheetProps} />
      {disabling && (
        <ConfirmDialog
          open
          onOpenChange={() => setDisabling(null)}
          title={`Disable ${disabling.tenantId}?`}
          description="The query engine stops routing queries to this tenant until it is enabled again."
          confirmLabel="Disable"
          destructive
          onConfirm={() => setEnabled.mutate({ tenantId: disabling.tenantId, isEnabled: false })}
        />
      )}
    </>
  );
}
