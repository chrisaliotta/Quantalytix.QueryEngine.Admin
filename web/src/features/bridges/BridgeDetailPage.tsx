import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { DroppedOnCloneAlert } from '@/features/mappings/DroppedOnCloneAlert';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { useBridge, useBridgeVersion, useDeleteBridge, useDeleteBridgeVersion } from './api';
import { BridgeIdentitySheet } from './BridgeIdentitySheet';
import { BridgeVersionPanel } from './BridgeVersionPanel';
import { bridgeVersionValues } from './bridgeVersionForm';
import { NewBridgeVersionDialog, type NewBridgeVersionValues } from './NewBridgeVersionDialog';
import type { Bridge, CreatedBridgeVersion } from './types';

/** One bridge: identity, version picker with lifecycle actions, and the selected version's settings and mappings. */
export function BridgeDetailPage() {
  const bridgeId = Number(useParams().bridgeId);
  const bridge = useBridge(bridgeId);

  if (bridge.isPending) {
    return <Loading />;
  }
  if (bridge.error) {
    return <ApiErrorAlert error={bridge.error} />;
  }
  return <BridgeDetail bridge={bridge.data} />;
}

function BridgeDetail({ bridge }: { bridge: Bridge }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('bridge-versions', bridge.bridgeId);
  const dimensions = useLookup('dimensions');
  const sourceVersions = useLookup('dimension-versions', bridge.sourceDimensionId);
  const targetVersions = useLookup('dimension-versions', bridge.targetDimensionId);
  const deleteBridge = useDeleteBridge();
  const deleteVersion = useDeleteBridgeVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewBridgeVersionValues | null>(null);
  const [dropped, setDropped] = useState<CreatedBridgeVersion | null>(null);

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
    setNewVersion({
      sourceDimensionVersionId: latestPublishedId(sourceVersions.data ?? []),
      targetDimensionVersionId: latestPublishedId(targetVersions.data ?? []),
      cardinality: 'many_to_one',
      allocationMode: 'none',
      cloneFromVersionId: null,
    });
  }

  function openClone(sourceId: number) {
    if (version.data?.bridgeVersionId === sourceId) {
      setNewVersion({ ...bridgeVersionValues(version.data), cloneFromVersionId: sourceId });
    }
  }

  function showCreated(created: CreatedBridgeVersion) {
    toast.success(`Created draft v${created.version.versionNumber}.`);
    selectVersion(created.version.bridgeVersionId);
    if (created.droppedMappings.length > 0) {
      setDropped(created);
    }
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

      {/* Keyed by version: a rejected transition of one version must not stay on screen for the next. */}
      <VersionHeader
        key={versionId}
        kind="bridge_version"
        versions={versionItems.map(item => ({ ...item, label: versionLabel(item, bridge.currentBridgeVersionId) }))}
        selectedVersionId={versionId}
        onSelectVersion={selectVersion}
        onCreateVersion={openNew}
        onCloneVersion={openClone}
        onDeleteVersion={draftId =>
          deleteVersion.mutate(draftId, { onSuccess: () => setSearchParams({}, { replace: true }) })
        }
      />
      <ApiErrorAlert error={versions.error ?? version.error ?? deleteVersion.error} />
      {dropped && (
        <DroppedOnCloneAlert
          title={`v${dropped.version.versionNumber} left out ${dropped.droppedMappings.length} of the cloned mappings`}
          description="The new endpoint versions have no member with the source or target key of these mappings:"
          items={dropped.droppedMappings.map(mapping => `${mapping.sourceMemberKey} → ${mapping.targetMemberKey}`)}
          onDismiss={() => setDropped(null)}
        />
      )}
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version pins a version of each dimension, the allocation semantics and the mappings.
        </p>
      )}

      {selected && version.data && (
        <BridgeVersionPanel
          key={version.data.bridgeVersionId}
          bridge={bridge}
          version={version.data}
          isDraft={selected.status === 'draft'}
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
        onConfirm={() => deleteBridge.mutate(bridge.bridgeId, { onSuccess: () => navigate(`/t/${tenantId}/bridges`) })}
      />
    </div>
  );
}

/** A version's picker label: its allocation mode, and whether it is the bridge's current version. */
function versionLabel(version: LookupItem, currentVersionId: number | null | undefined) {
  const allocationMode = version.classifier ?? version.label;
  if (version.id === currentVersionId) {
    return `${allocationMode} · current`;
  }
  return allocationMode;
}

/** The latest published version of a dimension, which a new bridge version pins by default. */
function latestPublishedId(versions: LookupItem[]) {
  return versions.findLast(version => version.status === 'published')?.id ?? null;
}
