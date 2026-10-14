import { Building2 } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router';
import { Combobox } from '@registry/components/Combobox';
import { Badge } from '@/components/ui/badge';
import { useSignedInSession } from '@/auth/useSession';
import { useTenantId } from '@/shared/useTenantId';
import { useTenants } from '@/shared/useTenants';

/** SystemAdmins pick any tenant (staying on the same page); other users see their fixed tenant. */
export function TenantSwitcher() {
  const { me } = useSignedInSession();
  if (!me.isSystemAdmin) {
    return <TenantBadge />;
  }
  return <TenantCombobox />;
}

function TenantBadge() {
  const tenantId = useTenantId();
  return (
    <Badge variant="outline" data-slot="tenant-badge" className="h-8 gap-1.5 px-2.5">
      <Building2 />
      {tenantId}
    </Badge>
  );
}

function TenantCombobox() {
  const tenantId = useTenantId();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const tenants = useTenants();

  if (!tenants.data) {
    return <TenantBadge />;
  }

  return (
    <Combobox
      className="w-52"
      size="sm"
      data={tenants.data.map(tenant => ({
        value: tenant.tenantId,
        label: tenant.isEnabled ? tenant.tenantId : `${tenant.tenantId} (disabled)`,
      }))}
      value={tenantId}
      onValueChange={next => navigate(pathname.replace(`/t/${tenantId}`, `/t/${next}`))}
      placeholder="Select a tenant"
      searchPlaceholder="Search tenants..."
      emptyMessage="No tenants found."
    />
  );
}
