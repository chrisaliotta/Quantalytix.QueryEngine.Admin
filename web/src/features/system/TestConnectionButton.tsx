import { CircleCheck, CircleX, PlugZap } from 'lucide-react';
import { useFormContext, useWatch } from 'react-hook-form';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import type { TenantConnectionCheck } from './types';
import { useVerifyTenantConnection } from './useSystemTenants';

/** The form values a connection test reads. */
interface ConnectionValues {
  tenantId: string;
  connectionString: string;
}

/**
 * "Test connection" for a tenant sheet: verifies the typed connection string for the form's tenant
 * ID without saving it. The outcome shows until either value changes.
 */
export function TestConnectionButton() {
  const { control } = useFormContext<ConnectionValues>();
  const [tenantId, connectionString] = useWatch({ control, name: ['tenantId', 'connectionString'] });
  const verify = useVerifyTenantConnection();
  const isCurrent = verify.variables?.tenantId === tenantId && verify.variables?.connectionString === connectionString;

  return (
    <div data-slot="test-connection" className="space-y-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!connectionString || verify.isPending}
        onClick={() => verify.mutate({ tenantId, connectionString })}
      >
        <PlugZap />
        Test connection
      </Button>
      {isCurrent && verify.data && <ConnectionCheckResult check={verify.data} />}
      {isCurrent && <ApiErrorAlert error={verify.error} />}
    </div>
  );
}

function ConnectionCheckResult({ check }: { check: TenantConnectionCheck }) {
  if (!check.isValid) {
    return (
      <Alert variant="destructive">
        <CircleX />
        <AlertDescription>{check.message}</AlertDescription>
      </Alert>
    );
  }
  return (
    <Alert>
      <CircleCheck className="text-positive" />
      <AlertDescription>{check.message}</AlertDescription>
    </Alert>
  );
}
