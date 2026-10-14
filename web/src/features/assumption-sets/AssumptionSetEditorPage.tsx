import { Camera, Pencil } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { formatDateTime } from '@/features/releases/formatDateTime';
import { PinnedVersionsTable } from '@/features/releases/PinnedVersionsTable';
import { useSheetForm } from '@/features/releases/useSheetForm';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { PageHeader } from '@/shared/PageHeader';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { AssumptionSetSettingsSheet } from './AssumptionSetSettingsSheet';
import { CaptureDialog } from './CaptureDialog';
import type { AssumptionSet, AssumptionSetUpdateInput } from './types';
import { useAssumptionPins, useAssumptionSet, useDeleteAssumptionSet, useSaveAssumptionPins } from './useAssumptionSets';

/**
 * One assumption set: its lifecycle (publish, retire, delete draft), its settings, and the
 * dimension versions it pins. Settings and pins change only while the set is a draft.
 */
export function AssumptionSetEditorPage() {
  const assumptionSetId = Number(useParams().assumptionSetId);
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const versions = useLookup('assumption-sets');
  const set = useAssumptionSet(assumptionSetId);
  const deleteSet = useDeleteAssumptionSet();

  return (
    <div data-slot="assumption-set-editor" className="space-y-6">
      <PageHeader
        title={set.data?.displayName ?? 'Assumption set'}
        description="Pins exact dimension versions; once published, the snapshot never changes."
      />
      <VersionHeader
        kind="assumption_set"
        versions={versions.data ?? []}
        selectedVersionId={assumptionSetId}
        onSelectVersion={id => navigate(`/t/${tenantId}/assumption-sets/${id}`)}
        onDeleteVersion={id => deleteSet.mutate(id, { onSuccess: () => navigate(`/t/${tenantId}/assumption-sets`) })}
      />
      <ApiErrorAlert error={set.error ?? deleteSet.error} />
      {set.data && <SettingsCard assumptionSet={set.data} />}
      {set.data && <PinsCard assumptionSet={set.data} />}
    </div>
  );
}

function SettingsCard({ assumptionSet }: { assumptionSet: AssumptionSet }) {
  const settings = useSheetForm<AssumptionSetUpdateInput>({ displayName: '', description: '', effectiveAt: '' });

  function openSettings() {
    settings.open({
      displayName: assumptionSet.displayName,
      description: assumptionSet.description ?? '',
      effectiveAt: assumptionSet.effectiveAt,
    });
  }

  return (
    <Card data-slot="assumption-set-settings">
      <CardHeader>
        <CardTitle>Settings</CardTitle>
        <CardDescription>The effective time decides which versions a capture pins.</CardDescription>
        {assumptionSet.status === 'draft' && (
          <CardAction>
            <Button variant="outline" size="sm" onClick={openSettings}>
              <Pencil />
              Edit settings
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[max-content_1fr]">
          <Setting label="Key">{assumptionSet.assumptionSetKey}</Setting>
          <Setting label="Display name">{assumptionSet.displayName}</Setting>
          <Setting label="Description">{assumptionSet.description || '—'}</Setting>
          <Setting label="Effective at">{formatDateTime(assumptionSet.effectiveAt)}</Setting>
          <Setting label="Created">{formatDateTime(assumptionSet.createdAt)}</Setting>
          <Setting label="Published">{formatDateTime(assumptionSet.publishedAt)}</Setting>
        </dl>
      </CardContent>
      <AssumptionSetSettingsSheet assumptionSetId={assumptionSet.assumptionSetId} {...settings.sheetProps} />
    </Card>
  );
}

function Setting({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </>
  );
}

function PinsCard({ assumptionSet }: { assumptionSet: AssumptionSet }) {
  const [isCapturing, setIsCapturing] = useState(false);
  const isDraft = assumptionSet.status === 'draft';
  return (
    <Card data-slot="assumption-set-pins">
      <CardHeader>
        <CardTitle>Pinned dimension versions</CardTitle>
        <CardDescription>
          {isDraft && 'Capture the versions effective at the set\'s effective time, or pin published versions by hand, then save.'}
          {!isDraft && 'The pins are fixed once the set is published.'}
        </CardDescription>
        {isDraft && (
          <CardAction>
            <Button variant="outline" size="sm" onClick={() => setIsCapturing(true)}>
              <Camera />
              Capture effective versions
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        <PinsTable assumptionSetId={assumptionSet.assumptionSetId} isDraft={isDraft} />
      </CardContent>
      <CaptureDialog assumptionSet={assumptionSet} open={isCapturing} onOpenChange={setIsCapturing} />
    </Card>
  );
}

function PinsTable({ assumptionSetId, isDraft }: { assumptionSetId: number; isDraft: boolean }) {
  const pins = useAssumptionPins(assumptionSetId);
  const save = useSaveAssumptionPins(assumptionSetId);

  if (pins.isPending) {
    return <Loading size="sm" />;
  }
  if (pins.isError) {
    return <ApiErrorAlert error={pins.error} />;
  }
  return (
    <PinnedVersionsTable
      key={`${assumptionSetId}:${pins.dataUpdatedAt}`}
      identityLookup="dimensions"
      versionLookup="dimension-versions"
      identityLabel="Dimension"
      pins={pins.data.map(pin => ({ identityId: pin.dimensionId, versionId: pin.dimensionVersionId }))}
      readOnly={!isDraft}
      onSave={rows =>
        save.mutate(
          rows.map(row => ({ dimensionId: row.identityId, dimensionVersionId: row.versionId })),
          { onSuccess: () => toast.success('Pins saved.') }
        )
      }
      isSaving={save.isPending}
      saveError={save.error}
    />
  );
}
