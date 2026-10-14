import { Navigate } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { ErrorPage } from '@registry/pages/ErrorPage';
import { useSignedInSession } from '@/auth/useSession';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useTenants } from '@/shared/useTenants';

/** `/`: opens the overview of the session's tenant, or of the first accessible tenant. */
export function TenantRedirect() {
  const { me } = useSignedInSession();
  if (me.tenantId) {
    return <Navigate to={`/t/${me.tenantId}/overview`} replace />;
  }
  return <FirstTenantRedirect />;
}

function FirstTenantRedirect() {
  const tenants = useTenants();

  if (tenants.isPending) {
    return <Loading className="h-screen" description="Loading tenants..." />;
  }
  if (tenants.error) {
    return (
      <div className="p-6">
        <ApiErrorAlert error={tenants.error} />
      </div>
    );
  }
  const [first] = tenants.data;
  if (!first) {
    return (
      <ErrorPage statusCode={404} title="No tenants" description="There is no tenant to administer yet." />
    );
  }
  return <Navigate to={`/t/${first.tenantId}/overview`} replace />;
}
