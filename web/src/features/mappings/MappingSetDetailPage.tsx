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
import type { LookupItem, VersionStatus } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { isEditable } from '@/shared/versionTransitions';
import { useDeleteMappingSet, useDeleteMappingVersion, useMappingSet } from './api';
import { MappingEntries } from './MappingEntries';
import { MappingSetIdentitySheet } from './MappingSetIdentitySheet';
import { NewMappingVersionDialog } from './NewMappingVersionDialog';
import type { MappingSet, MappingVersion } from './types';

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

  const versionItems = versions.data ?? [];
  const dimensionVersionItems = dimensionVersions.data ?? [];
  const selected = selectedVersion(versionItems, searchParams.get('version'));
  const versionId = selected?.id ?? null;
  const published = dimensionVersionItems.find(version => version.status === 'published');
  const dimensionKey = dimensions.data?.find(dimension => dimension.id === mappingSet.dimensionId)?.key ?? '';

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) }, { replace: true });
  }

  function showCreated(created: MappingVersion) {
    toast.success(`Created draft v${created.versionNumber}.`);
    selectVersion(created.mappingVersionId);
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

      <VersionHeader
        kind="mapping_version"
        versions={versionItems}
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
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version holds the entries that map source keys onto members of {dimensionKey}; the
          published version is the one requests read.
        </p>
      )}

      {selected && (
        <div className="space-y-3">
          <MembersSource dimensionId={mappingSet.dimensionId} dimensionKey={dimensionKey} published={published} />
          <MappingEntries
            key={selected.id}
            versionId={selected.id}
            dimensionId={mappingSet.dimensionId}
            isEditable={isEditable('mapping_version', selected.status as VersionStatus)}
          />
        </div>
      )}

      {editingIdentity && <MappingSetIdentitySheet mappingSet={mappingSet} onClose={() => setEditingIdentity(false)} />}
      {newVersion && (
        <NewMappingVersionDialog
          mappingSet={mappingSet}
          source={newVersion.source}
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

interface MembersSourceProps {
  dimensionId: number;
  dimensionKey: string;
  /** The dimension's published version, whose members the entries' keys resolve in; undefined while it has none. */
  published: LookupItem | undefined;
}

/** The dimension version the entries' member keys resolve in, its published one, with a link to it. */
function MembersSource({ dimensionId, dimensionKey, published }: MembersSourceProps) {
  const tenantId = useTenantId();
  if (!published) {
    return (
      <p data-slot="members-source" className="text-sm text-muted-foreground">
        {dimensionKey} has no published version yet, so the member keys resolve in none.
      </p>
    );
  }
  return (
    <div data-slot="members-source" className="flex flex-wrap items-center gap-2 text-sm">
      <span className="text-muted-foreground">Member keys resolve in</span>
      <span className="font-medium">
        {dimensionKey} {published.label}
      </span>
      <StatusBadge status="published" />
      <Button asChild variant="link" size="sm">
        <Link to={`/t/${tenantId}/dimensions/${dimensionId}?version=${published.id}`}>
          Open dimension
          <ExternalLink />
        </Link>
      </Button>
    </div>
  );
}
