import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { dimensionPath } from '@/features/dimensions/paths';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import type { VersionStatus } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { isEditable } from '@/shared/versionTransitions';
import { useBridge, useBridgeVersion, useDeleteBridge, useDeleteBridgeVersion } from './api';
import { BridgeIdentitySheet } from './BridgeIdentitySheet';
import { BridgeVersionPanel } from './BridgeVersionPanel';
import { bridgeVersionValues } from './bridgeVersionForm';
import { NewBridgeVersionDialog, type NewBridgeVersionValues } from './NewBridgeVersionDialog';
import type { Bridge, BridgeVersion } from './types';

/**
 * One analytical bridge, under the dimension it leads into: identity, version picker with lifecycle actions, and the selected
 * version's settings and mappings. A report bridge has no page; its report's editor edits it.
 */
export function BridgeDetailPage() {
  const bridgeId = Number(useParams().bridgeId);
  const summary = useBridge(bridgeId);

  if (summary.isPending) {
    return <Loading />;
  }
  if (summary.error) {
    return <ApiErrorAlert error={summary.error} />;
  }
  return <BridgeDetail bridge={summary.data.bridge} />;
}

function BridgeDetail({ bridge }: { bridge: Bridge }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('bridge-versions', bridge.bridgeId);
  const dimensions = useLookup('dimensions');
  const deleteBridge = useDeleteBridge();
  const deleteVersion = useDeleteBridgeVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewBridgeVersionValues | null>(null);

  const versionItems = versions.data ?? [];
  const selected = selectedVersion(versionItems, searchParams.get('version'));
  const versionId = selected?.id ?? null;
  const version = useBridgeVersion(versionId);
  const dimensionKeys = new Map((dimensions.data ?? []).map(dimension => [dimension.id, dimension.key]));
  const sourceKey = dimensionKeys.get(bridge.sourceDimensionId) ?? '';
  const targetKey = dimensionKeys.get(bridge.targetDimensionId) ?? '';

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) }, { replace: true });
  }

  function openNew() {
    setNewVersion({ cardinality: 'many_to_one', allocationMode: 'none', cloneFromVersionId: null });
  }

  function openClone(sourceId: number) {
    if (version.data?.bridgeVersionId === sourceId) {
      setNewVersion({ ...bridgeVersionValues(version.data), cloneFromVersionId: sourceId });
    }
  }

  function showCreated(created: BridgeVersion) {
    toast.success(`Created draft v${created.versionNumber}.`);
    selectVersion(created.bridgeVersionId);
  }

  return (
    <div data-slot="bridge-detail" className="space-y-6">
      <PageHeader
        title={bridge.displayName}
        description={`${bridge.bridgeKey} · ${sourceKey} → ${targetKey}`}
        actions={
          <>
            <EnabledBadge enabled={bridge.isEnabled} />
            <Button variant="outline" onClick={() => setEditingIdentity(true)}>
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
      <ApiErrorAlert error={deleteBridge.error} />

      <VersionHeader
        kind="bridge_version"
        versions={versionItems.map(item => ({ ...item, label: item.classifier ?? item.label }))}
        selectedVersionId={versionId}
        onSelectVersion={selectVersion}
        onCreateVersion={openNew}
        onCloneVersion={openClone}
        onDeleteVersion={draftId => deleteVersion.mutate(draftId, { onSuccess: () => setSearchParams({}, { replace: true }) })}
      />
      <ApiErrorAlert error={versions.error ?? version.error ?? deleteVersion.error} />
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version holds the allocation semantics and the mappings; the published version is the one
          requests read.
        </p>
      )}

      {selected && version.data && (
        <BridgeVersionPanel
          key={version.data.bridgeVersionId}
          bridge={bridge}
          version={version.data}
          isEditable={isEditable('bridge_version', selected.status as VersionStatus)}
        />
      )}

      {editingIdentity && <BridgeIdentitySheet bridge={bridge} onClose={() => setEditingIdentity(false)} />}
      {newVersion && (
        <NewBridgeVersionDialog
          bridge={bridge}
          defaults={newVersion}
          onClose={() => setNewVersion(null)}
          onCreated={showCreated}
        />
      )}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${bridge.bridgeKey}?`}
        description="The bridge and its draft versions are deleted. A bridge with validated, published or retired versions, or one a binding uses, is kept."
        confirmLabel="Delete bridge"
        destructive
        onConfirm={() =>
          deleteBridge.mutate(bridge.bridgeId, {
            onSuccess: () => navigate(dimensionPath(tenantId, bridge.targetDimensionId, 'bridges')),
          })
        }
      />
    </div>
  );
}
