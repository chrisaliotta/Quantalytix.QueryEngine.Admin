import { useMutation } from '@tanstack/react-query';
import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { toast } from 'sonner';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import { useTenantId } from '@/shared/useTenantId';
import { DatasetEditSheet } from './DatasetEditSheet';
import type { Dataset } from './types';

/**
 * Identity header of a dataset page: key, display name and availability, with an edit sheet and a delete that the API
 * refuses (shown inline) while a version is not a draft or a release lists the dataset.
 */
export function DatasetHeader({ dataset }: { dataset: Dataset }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const remove = useMutation({
    mutationFn: async () =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/datasets/{datasetId}', {
          params: { path: { tenantId, datasetId: dataset.datasetId } },
        })
      ),
    onSuccess: () => {
      toast.success(`Deleted ${dataset.datasetKey}.`);
      navigate('..');
      return invalidateTenantQueries(tenantId);
    },
  });

  return (
    <div data-slot="dataset-header" className="space-y-3">
      <PageHeader
        title={dataset.datasetKey}
        description={dataset.displayName}
        actions={
          <>
            <EnabledBadge enabled={dataset.isEnabled} />
            <Button variant="outline" onClick={() => setEditing(true)}>
              <Pencil />
              Edit
            </Button>
            <Button variant="outline" className="text-destructive" onClick={() => setConfirmingDelete(true)}>
              <Trash2 />
              Delete
            </Button>
          </>
        }
      />
      <ApiErrorAlert error={remove.error} />
      {editing && <DatasetEditSheet dataset={dataset} onClose={() => setEditing(false)} />}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title="Delete this dataset?"
        description={`${dataset.datasetKey} and all its draft versions will be deleted. Datasets with validated, published or retired versions, or listed by a release, are kept.`}
        confirmLabel="Delete dataset"
        destructive
        onConfirm={() => remove.mutate()}
      />
    </div>
  );
}
