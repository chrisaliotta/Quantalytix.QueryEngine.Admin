import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { PageHeader } from '@/shared/PageHeader';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { useDeleteDimension, useDeleteDimensionVersion, useDimension } from './api';
import { DimensionIdentitySheet } from './DimensionIdentitySheet';
import { LevelsTab } from './LevelsTab';
import { MappingVersionsTab } from './MappingVersionsTab';
import { MembersTab } from './MembersTab';
import { NewVersionSheet } from './NewVersionSheet';
import { RangeBandsTab } from './RangeBandsTab';
import { SettingsTab } from './SettingsTab';
import type { Dimension } from './types';

/** What the new-version sheet was opened for: an empty draft, or a clone of a version. */
interface NewVersionRequest {
  source: LookupItem | null;
}

/** One dimension: identity, version picker with lifecycle actions, and the selected version's content in tabs. */
export function DimensionDetailPage() {
  const dimensionId = Number(useParams().dimensionId);
  const dimension = useDimension(dimensionId);

  if (dimension.isPending) {
    return <Loading />;
  }
  if (dimension.error) {
    return <ApiErrorAlert error={dimension.error} />;
  }
  return <DimensionDetail dimension={dimension.data} />;
}

function DimensionDetail({ dimension }: { dimension: Dimension }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('dimension-versions', dimension.dimensionId);
  const deleteDimension = useDeleteDimension();
  const deleteVersion = useDeleteDimensionVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewVersionRequest | null>(null);

  const versionList = versions.data ?? [];
  const selected = selectedVersion(versionList, searchParams.get('version'));
  const isRange = dimension.dimensionKind === 'range';
  const isDraft = selected?.status === 'draft';

  function selectVersion(versionId: number) {
    setSearchParams({ version: String(versionId) }, { replace: true });
  }

  return (
    <div data-slot="dimension-detail" className="space-y-6">
      <PageHeader
        title={dimension.displayName}
        description={`${dimension.dimensionKey} · ${dimension.dimensionKind} · ${dimension.definitionOrigin}`}
        actions={
          <>
            {!dimension.isEnabled && <Badge variant="outline">Disabled</Badge>}
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
      <ApiErrorAlert error={deleteDimension.error} />

      <VersionHeader
        kind="dimension_version"
        versions={versionList}
        selectedVersionId={selected?.id ?? null}
        onSelectVersion={selectVersion}
        onCreateVersion={() => setNewVersion({ source: null })}
        onCloneVersion={sourceId => setNewVersion({ source: versionList.find(version => version.id === sourceId) ?? null })}
        onDeleteVersion={versionId =>
          deleteVersion.mutate(versionId, { onSuccess: () => setSearchParams({}, { replace: true }) })
        }
      />
      <ApiErrorAlert error={deleteVersion.error} />

      {selected && (
        <Tabs defaultValue="members">
          <TabsList>
            <TabsTrigger value="members">Members</TabsTrigger>
            <TabsTrigger value="levels">Levels</TabsTrigger>
            {isRange && <TabsTrigger value="range-bands">Range bands</TabsTrigger>}
            <TabsTrigger value="settings">Settings</TabsTrigger>
            <TabsTrigger value="mapping-versions">Mapping versions</TabsTrigger>
          </TabsList>
          <TabsContent value="members">
            <MembersTab key={selected.id} dimension={dimension} versionId={selected.id} isDraft={isDraft} />
          </TabsContent>
          <TabsContent value="levels">
            <LevelsTab key={selected.id} versionId={selected.id} isDraft={isDraft} />
          </TabsContent>
          {isRange && (
            <TabsContent value="range-bands">
              <RangeBandsTab key={selected.id} versionId={selected.id} rangeDataType={selected.classifier ?? null} isDraft={isDraft} />
            </TabsContent>
          )}
          <TabsContent value="settings">
            <SettingsTab key={selected.id} versionId={selected.id} isRange={isRange} isDraft={isDraft} />
          </TabsContent>
          <TabsContent value="mapping-versions">
            <MappingVersionsTab dimensionId={dimension.dimensionId} versionId={selected.id} versionKey={selected.key} />
          </TabsContent>
        </Tabs>
      )}

      {editingIdentity && <DimensionIdentitySheet dimension={dimension} onClose={() => setEditingIdentity(false)} />}
      {newVersion && (
        <NewVersionSheet
          dimensionId={dimension.dimensionId}
          isRange={isRange}
          source={newVersion.source}
          rangeDataType={versionList.at(-1)?.classifier ?? null}
          onClose={() => setNewVersion(null)}
          onCreated={version => selectVersion(version.dimensionVersionId)}
        />
      )}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${dimension.dimensionKey}?`}
        description="The dimension and its draft versions are deleted. Dimensions with published versions or references cannot be deleted."
        confirmLabel="Delete dimension"
        destructive
        onConfirm={() =>
          deleteDimension.mutate(dimension.dimensionId, { onSuccess: () => navigate(`/t/${tenantId}/dimensions`) })
        }
      />
    </div>
  );
}

/** The version named by `?version=`, or the latest one when the parameter names none of the dimension's versions. */
function selectedVersion(versions: LookupItem[], requested: string | null): LookupItem | null {
  const match = versions.find(version => String(version.id) === requested);
  if (match) {
    return match;
  }
  return versions.at(-1) ?? null;
}
