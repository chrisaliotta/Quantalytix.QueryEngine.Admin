import { ExternalLink, Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { PageHeader } from '@/shared/PageHeader';
import { StatusBadge } from '@/shared/StatusBadge';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { useDeleteMappingSet, useDeleteMappingVersion, useMappingSet } from './api';
import { DroppedOnCloneAlert } from './DroppedOnCloneAlert';
import { MappingEntries } from './MappingEntries';
import { MappingSetIdentitySheet } from './MappingSetIdentitySheet';
import { NewMappingVersionDialog } from './NewMappingVersionDialog';
import type { CreatedMappingVersion, MappingSet } from './types';

/** What the new-version dialog was opened for: an empty draft, or a clone of a version. */
interface NewVersionRequest {
  source: LookupItem | null;
}

/** One mapping set: identity, version picker with lifecycle actions, and the selected version's entries. */
export function MappingSetDetailPage() {
  const mappingSetId = Number(useParams().mappingSetId);
  const mappingSet = useMappingSet(mappingSetId);

  if (mappingSet.isPending) {
    return <Loading />;
  }
  if (mappingSet.error) {
    return <ApiErrorAlert error={mappingSet.error} />;
  }
  return <MappingSetDetail mappingSet={mappingSet.data} />;
}

function MappingSetDetail({ mappingSet }: { mappingSet: MappingSet }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('mapping-versions', mappingSet.mappingSetId);
  const dimensions = useLookup('dimensions');
  const dimensionVersions = useLookup('dimension-versions', mappingSet.dimensionId);
  const deleteMappingSet = useDeleteMappingSet();
  const deleteVersion = useDeleteMappingVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewVersionRequest | null>(null);
  const [dropped, setDropped] = useState<CreatedMappingVersion | null>(null);

  const versionItems = versions.data ?? [];
  const dimensionVersionItems = dimensionVersions.data ?? [];
  const selected = selectedVersion(versionItems, searchParams.get('version'));
  const versionId = selected?.id ?? null;
  const pinned = dimensionVersionItems.find(version => version.id === selected?.parentId);
  const dimensionKey = dimensions.data?.find(dimension => dimension.id === mappingSet.dimensionId)?.key ?? '';

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) }, { replace: true });
  }

  function showCreated(created: CreatedMappingVersion) {
    toast.success(`Created draft v${created.version.versionNumber}.`);
    selectVersion(created.version.mappingVersionId);
    if (created.droppedSourceKeys.length > 0) {
      setDropped(created);
    }
  }

  return (
    <div data-slot="mapping-set-detail" className="space-y-6">
      <PageHeader
        title={mappingSet.displayName}
        description={`${mappingSet.mappingSetKey} · ${mappingSet.sourceNamespace} → ${dimensionKey}`}
        actions={
          <>
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
      <ApiErrorAlert error={deleteMappingSet.error} />

      {/* Keyed by version: a rejected transition of one version must not stay on screen for the next. */}
      <VersionHeader
        key={versionId}
        kind="mapping_version"
        versions={versionItems.map(version => ({ ...version, label: pinnedLabel(version, dimensionVersionItems) }))}
        selectedVersionId={versionId}
        onSelectVersion={selectVersion}
        onCreateVersion={() => setNewVersion({ source: null })}
        onCloneVersion={sourceId =>
          setNewVersion({ source: versionItems.find(version => version.id === sourceId) ?? null })
        }
        onDeleteVersion={draftId =>
          deleteVersion.mutate(draftId, { onSuccess: () => setSearchParams({}, { replace: true }) })
        }
      />
      <ApiErrorAlert error={versions.error ?? deleteVersion.error} />
      {dropped && (
        <DroppedOnCloneAlert
          title={`v${dropped.version.versionNumber} left out ${dropped.droppedSourceKeys.length} of the cloned entries`}
          description="No member of the pinned dimension version has the member key of these source keys:"
          items={dropped.droppedSourceKeys}
          onDismiss={() => setDropped(null)}
        />
      )}
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version pins a dimension version and holds the entries that map source keys onto its
          members.
        </p>
      )}

      {selected && (
        <div className="space-y-3">
          {pinned && <PinnedVersion dimensionId={mappingSet.dimensionId} dimensionKey={dimensionKey} pinned={pinned} />}
          <MappingEntries
            key={selected.id}
            versionId={selected.id}
            dimensionVersionId={selected.parentId ?? null}
            isDraft={selected.status === 'draft'}
          />
        </div>
      )}

      {editingIdentity && <MappingSetIdentitySheet mappingSet={mappingSet} onClose={() => setEditingIdentity(false)} />}
      {newVersion && (
        <NewMappingVersionDialog
          mappingSet={mappingSet}
          source={newVersion.source}
          dimensionVersionId={dimensionVersionItems.findLast(version => version.status === 'published')?.id ?? null}
          onClose={() => setNewVersion(null)}
          onCreated={showCreated}
        />
      )}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${mappingSet.mappingSetKey}?`}
        description="The mapping set and its draft versions are deleted. A set with validated, published or retired versions, or one a binding uses, is kept."
        confirmLabel="Delete mapping set"
        destructive
        onConfirm={() =>
          deleteMappingSet.mutate(mappingSet.mappingSetId, { onSuccess: () => navigate(`/t/${tenantId}/mappings`) })
        }
      />
    </div>
  );
}

/** A version's picker label: the dimension version it pins, such as `pins v2 [2026-01-01 .. open)`. */
function pinnedLabel(version: LookupItem, dimensionVersions: LookupItem[]) {
  const pinned = dimensionVersions.find(dimensionVersion => dimensionVersion.id === version.parentId);
  if (!pinned) {
    return version.label;
  }
  return `pins ${pinned.label}`;
}

interface PinnedVersionProps {
  dimensionId: number;
  dimensionKey: string;
  /** The dimension version the selected mapping version pins. */
  pinned: LookupItem;
}

/** The dimension version the selected mapping version pins, with its status and a link to it. */
function PinnedVersion({ dimensionId, dimensionKey, pinned }: PinnedVersionProps) {
  const tenantId = useTenantId();
  return (
    <div data-slot="pinned-dimension-version" className="flex flex-wrap items-center gap-2 text-sm">
      <span className="text-muted-foreground">Maps onto</span>
      <span className="font-medium">
        {dimensionKey} {pinned.label}
      </span>
      {pinned.status && <StatusBadge status={pinned.status} />}
      <Button asChild variant="link" size="sm">
        <Link to={`/t/${tenantId}/dimensions/${dimensionId}?version=${pinned.id}`}>
          Open dimension
          <ExternalLink />
        </Link>
      </Button>
    </div>
  );
}
