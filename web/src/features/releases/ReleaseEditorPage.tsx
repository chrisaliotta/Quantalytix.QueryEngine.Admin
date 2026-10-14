import { TriangleAlert } from 'lucide-react';
import { useNavigate, useParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { PageHeader } from '@/shared/PageHeader';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { ActivateReleaseButton } from './ActivateReleaseButton';
import { formatDateTime } from './formatDateTime';
import { NewReleaseSheet } from './NewReleaseSheet';
import { PinnedVersionsTable } from './PinnedVersionsTable';
import type { ReleaseCreateInput, SemanticRelease } from './types';
import {
  useActivateRelease,
  useDeleteRelease,
  useRelease,
  useReleaseDatasets,
  useReleases,
  useSaveReleaseDatasets,
} from './useReleases';
import { useSheetForm } from './useSheetForm';

/**
 * One release: its lifecycle (validate, publish, retire, delete draft), activation, and the
 * manifest of pinned dataset versions, which only a draft can change. "Clone selected" creates a
 * new draft that copies the selected release's manifest.
 */
export function ReleaseEditorPage() {
  const releaseId = Number(useParams().releaseId);
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const versions = useLookup('releases');
  const summaries = useReleases();
  const release = useRelease(releaseId);
  const activate = useActivateRelease();
  const deleteRelease = useDeleteRelease();
  const newRelease = useSheetForm<ReleaseCreateInput>({ releaseKey: '', copyFromReleaseId: null });
  const isActive = summaries.data?.some(summary => summary.isActive && summary.release.releaseId === releaseId) ?? false;

  function activateRelease(selected: SemanticRelease) {
    activate.mutate(selected.releaseId, {
      onSuccess: () => toast.success(`${selected.releaseKey} is now the active release.`),
    });
  }

  return (
    <div data-slot="release-editor" className="space-y-6">
      <PageHeader
        title={release.data?.releaseKey ?? 'Release'}
        description="Validate and publish the release, then activate it so queries use its dataset versions."
        actions={
          release.data && (
            <ReleaseActivation
              release={release.data}
              isActive={isActive}
              isPending={activate.isPending}
              onActivate={activateRelease}
            />
          )
        }
      />
      <VersionHeader
        kind="semantic_release"
        versions={versions.data ?? []}
        selectedVersionId={releaseId}
        onSelectVersion={id => navigate(`/t/${tenantId}/releases/${id}`)}
        onCloneVersion={sourceId => newRelease.open({ releaseKey: '', copyFromReleaseId: sourceId })}
        onDeleteVersion={id => deleteRelease.mutate(id, { onSuccess: () => navigate(`/t/${tenantId}/releases`) })}
      />
      {isActive && release.data?.status === 'published' && <ActiveReleaseRetireWarning />}
      <ApiErrorAlert error={release.error ?? activate.error ?? deleteRelease.error} />
      {release.data && <ManifestCard release={release.data} />}
      <NewReleaseSheet {...newRelease.sheetProps} />
    </div>
  );
}

/** The active badge, or "Activate" for a published release that is not active. */
function ReleaseActivation(props: {
  release: SemanticRelease;
  isActive: boolean;
  isPending: boolean;
  onActivate: (release: SemanticRelease) => void;
}) {
  if (props.isActive) {
    return <Badge>Active</Badge>;
  }
  if (props.release.status !== 'published') {
    return null;
  }
  return (
    <ActivateReleaseButton
      releaseKey={props.release.releaseKey}
      isPending={props.isPending}
      onActivate={() => props.onActivate(props.release)}
    />
  );
}

/** Shown on the active release: retiring it would leave queries without a published active release. */
function ActiveReleaseRetireWarning() {
  return (
    <Alert data-slot="active-release-retire-warning">
      <TriangleAlert className="text-amber-500" />
      <AlertTitle>This is the active release</AlertTitle>
      <AlertDescription>
        Queries use this release. Retiring it leaves the tenant without a published active release, so queries fail until
        another release is activated. Activate another release before you retire this one.
      </AlertDescription>
    </Alert>
  );
}

function ManifestCard({ release }: { release: SemanticRelease }) {
  const isDraft = release.status === 'draft';
  return (
    <Card data-slot="release-manifest">
      <CardHeader>
        <CardTitle>Manifest</CardTitle>
        <CardDescription>
          {isDraft && 'Pin one published version per dataset, then save. Validating checks the pins and their bindings.'}
          {!isDraft && `The manifest is fixed once a release leaves draft. Published ${formatDateTime(release.publishedAt)}.`}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ManifestTable releaseId={release.releaseId} isDraft={isDraft} />
      </CardContent>
    </Card>
  );
}

function ManifestTable({ releaseId, isDraft }: { releaseId: number; isDraft: boolean }) {
  const manifest = useReleaseDatasets(releaseId);
  const save = useSaveReleaseDatasets(releaseId);

  if (manifest.isPending) {
    return <Loading size="sm" />;
  }
  if (manifest.isError) {
    return <ApiErrorAlert error={manifest.error} />;
  }
  return (
    <PinnedVersionsTable
      key={`${releaseId}:${manifest.dataUpdatedAt}`}
      identityLookup="datasets"
      versionLookup="dataset-versions"
      identityLabel="Dataset"
      pins={manifest.data.map(row => ({ identityId: row.datasetId, versionId: row.datasetVersionId }))}
      readOnly={!isDraft}
      onSave={pins =>
        save.mutate(
          pins.map(pin => ({ datasetId: pin.identityId, datasetVersionId: pin.versionId })),
          { onSuccess: () => toast.success('Manifest saved.') }
        )
      }
      isSaving={save.isPending}
      saveError={save.error}
    />
  );
}
