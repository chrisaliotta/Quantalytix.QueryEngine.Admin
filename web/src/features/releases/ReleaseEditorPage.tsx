import { useNavigate, useParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { PageHeader } from '@/shared/PageHeader';
import { PinnedVersionsTable } from '@/shared/PinnedVersionsTable';
import { useLookup } from '@/shared/useLookup';
import { useSheetForm } from '@/shared/useSheetForm';
import { useTenantId } from '@/shared/useTenantId';
import { formatDateTime } from '@/shared/utcTime';
import { VersionHeader } from '@/shared/VersionHeader';
import { ActivateReleaseButton } from './ActivateReleaseButton';
import { NewReleaseSheet } from './NewReleaseSheet';
import type { ReleaseCreateInput, SemanticRelease } from './types';
import {
  useActivateRelease,
  useDeleteRelease,
  useRelease,
  useReleaseDatasets,
  useReleases,
  useSaveReleaseDatasets,
} from './useReleases';

/** The Retire confirmation of the active release, which the API refuses to retire because queries run on it. */
const ACTIVE_RELEASE_RETIREMENT =
  'This is the active release, so queries use it and it cannot be retired. Activate another release first, then retire this one.';

/**
 * One release: its lifecycle (validate, publish, retire, delete draft), activation, and the manifest of pinned dataset
 * versions, which only a draft can change. "Clone selected" creates a new draft that copies the selected release's
 * manifest. Reports are not pinned: they run against the active release, and validating a release checks every published
 * report against its dataset versions.
 */
export function ReleaseEditorPage() {
  const semanticReleaseId = Number(useParams().semanticReleaseId);
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const versions = useLookup('releases');
  const summaries = useReleases();
  const release = useRelease(semanticReleaseId);
  const activate = useActivateRelease();
  const deleteRelease = useDeleteRelease();
  const newRelease = useSheetForm<ReleaseCreateInput>({ releaseKey: '', copyFromSemanticReleaseId: null });
  const isActive = summaries.data?.some(summary => summary.isActive && summary.release.semanticReleaseId === semanticReleaseId) ?? false;

  function activateRelease(selected: SemanticRelease) {
    activate.mutate(selected.semanticReleaseId, {
      onSuccess: () => toast.success(`${selected.releaseKey} is now the active release.`),
    });
  }

  return (
    <div data-slot="release-editor" className="space-y-6">
      <PageHeader
        title={release.data?.releaseKey ?? 'Release'}
        description="Validate and publish the release, then activate it so queries and reports use its dataset versions."
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
        selectedVersionId={semanticReleaseId}
        onSelectVersion={id => navigate(`/t/${tenantId}/releases/${id}`)}
        onCloneVersion={sourceId => newRelease.open({ releaseKey: '', copyFromSemanticReleaseId: sourceId })}
        onDeleteVersion={id => deleteRelease.mutate(id, { onSuccess: () => navigate(`/t/${tenantId}/releases`) })}
        confirmDescriptions={isActive ? { retired: ACTIVE_RELEASE_RETIREMENT } : undefined}
      />
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
        <ManifestTable semanticReleaseId={release.semanticReleaseId} isDraft={isDraft} />
      </CardContent>
    </Card>
  );
}

function ManifestTable({ semanticReleaseId, isDraft }: { semanticReleaseId: number; isDraft: boolean }) {
  const manifest = useReleaseDatasets(semanticReleaseId);
  const save = useSaveReleaseDatasets(semanticReleaseId);

  if (manifest.isPending) {
    return <Loading size="sm" />;
  }
  if (manifest.isError) {
    return <ApiErrorAlert error={manifest.error} />;
  }
  return (
    <PinnedVersionsTable
      key={`${semanticReleaseId}:${manifest.dataUpdatedAt}`}
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
