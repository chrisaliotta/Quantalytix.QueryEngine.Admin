import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { DatasetHeader } from './DatasetHeader';
import { DatasetVersionTabs } from './DatasetVersionTabs';
import { NewVersionDialog } from './NewVersionDialog';
import { useDataset } from './queries';
import { selectVersionId } from './versionSelection';

/**
 * One dataset: its identity, its versions with their lifecycle actions, and the editor of the version that
 * `?version=` names (the latest by default).
 */
export function DatasetDetailPage() {
  const tenantId = useTenantId();
  const datasetId = Number(useParams().datasetId);
  const [searchParams, setSearchParams] = useSearchParams();
  const [creatingVersion, setCreatingVersion] = useState(false);
  const [tab, setTab] = useState('fields');
  const dataset = useDataset(datasetId);
  const versions = useLookup('dataset-versions', datasetId);
  const versionItems = versions.data ?? [];
  const versionId = selectVersionId(versionItems, searchParams.get('version'));
  const isDraft = versionItems.find(version => version.id === versionId)?.status === 'draft';

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) });
  }

  const cloneVersion = useMutation({
    mutationFn: async (sourceVersionId: number) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/datasets/{datasetId}/versions', {
          params: { path: { tenantId, datasetId } },
          body: { cloneFromVersionId: sourceVersionId },
        })
      ),
    onSuccess: version => {
      toast.success(`Created draft v${version.versionNumber}.`);
      selectVersion(version.datasetVersionId);
      return invalidateTenantQueries(tenantId);
    },
  });
  const deleteVersion = useMutation({
    mutationFn: async (draftId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/dataset-versions/{versionId}', {
          params: { path: { tenantId, versionId: draftId } },
        })
      ),
    onSuccess: () => {
      toast.success('Draft deleted.');
      setSearchParams({});
      return invalidateTenantQueries(tenantId);
    },
  });

  if (dataset.isPending) {
    return <Loading size="sm" />;
  }
  if (dataset.error) {
    return <ApiErrorAlert error={dataset.error} />;
  }

  return (
    <div data-slot="dataset-detail-page" className="space-y-6">
      <DatasetHeader dataset={dataset.data} />
      <VersionHeader
        kind="dataset_version"
        versions={versionItems}
        selectedVersionId={versionId}
        onSelectVersion={selectVersion}
        onCreateVersion={() => setCreatingVersion(true)}
        onCloneVersion={sourceVersionId => cloneVersion.mutate(sourceVersionId)}
        onDeleteVersion={draftId => deleteVersion.mutate(draftId)}
      />
      <ApiErrorAlert error={versions.error ?? cloneVersion.error ?? deleteVersion.error} />
      {versionId !== null && (
        <DatasetVersionTabs versionId={versionId} isDraft={isDraft} tab={tab} onTabChange={setTab} />
      )}
      <NewVersionDialog
        open={creatingVersion}
        onOpenChange={setCreatingVersion}
        datasetId={datasetId}
        onCreated={version => selectVersion(version.datasetVersionId)}
      />
    </div>
  );
}
