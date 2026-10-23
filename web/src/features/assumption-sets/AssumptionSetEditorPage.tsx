import { Camera, Pencil, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { PageHeader } from '@/shared/PageHeader';
import { StatusBadge } from '@/shared/StatusBadge';
import { useLookup } from '@/shared/useLookup';
import { useSheetForm } from '@/shared/useSheetForm';
import { useTenantId } from '@/shared/useTenantId';
import { formatDateTime } from '@/shared/utcTime';
import { AssumptionPinsTable } from './AssumptionPinsTable';
import { AssumptionSetSettingsSheet } from './AssumptionSetSettingsSheet';
import { CaptureSheet } from './CaptureSheet';
import type { AssumptionCaptureInput, AssumptionPin, AssumptionSet, AssumptionSetUpdateInput } from './types';
import { useAssumptionSet, useDeleteAssumptionSet } from './useAssumptionSets';

/**
 * One assumption set with its details and pins. The active set holds the default versions, follows every publish and reads the
 * active release, so it offers a capture; a historical set keeps the versions and the release it captured and can be deleted.
 * Either can be relabelled.
 */
export function AssumptionSetEditorPage() {
  const assumptionSetId = Number(useParams().assumptionSetId);
  const detail = useAssumptionSet(assumptionSetId);

  if (detail.isPending) {
    return <Loading />;
  }
  if (detail.error) {
    return <ApiErrorAlert error={detail.error} />;
  }
  return <AssumptionSetDetailView assumptionSet={detail.data.set} pins={detail.data.pins} />;
}

function AssumptionSetDetailView({ assumptionSet, pins }: { assumptionSet: AssumptionSet; pins: AssumptionPin[] }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const deleteSet = useDeleteAssumptionSet();
  const labels = useSheetForm<AssumptionSetUpdateInput>({ displayName: '', description: '' });
  const capture = useSheetForm<AssumptionCaptureInput>({ assumptionSetKey: '', displayName: '', description: '' });
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const releases = useLookup('releases');
  const isActive = assumptionSet.status === 'active';
  const release = releases.data?.find(item => item.id === assumptionSet.semanticReleaseId);

  return (
    <div data-slot="assumption-set-editor" className="space-y-6">
      <PageHeader
        title={assumptionSet.displayName}
        description={
          isActive
            ? 'Holds the default versions: every publish repoints it, and requests that name no set read it.'
            : `Captured ${formatDateTime(assumptionSet.capturedAt)}; requests that name ${assumptionSet.assumptionSetKey} read these versions over the release it captured.`
        }
        actions={
          <>
            <StatusBadge status={assumptionSet.status} />
            <Button
              variant="outline"
              onClick={() => labels.open({ displayName: assumptionSet.displayName, description: assumptionSet.description ?? '' })}
            >
              <Pencil />
              Edit labels
            </Button>
            {isActive && (
              <Button onClick={() => capture.open({ assumptionSetKey: '', displayName: '', description: '' })}>
                <Camera />
                Capture
              </Button>
            )}
            {!isActive && (
              <Button variant="outline" className="text-destructive" onClick={() => setConfirmingDelete(true)}>
                <Trash2 />
                Delete
              </Button>
            )}
          </>
        }
      />
      <ApiErrorAlert error={deleteSet.error} />
      <Card data-slot="assumption-set-settings">
        <CardHeader>
          <CardTitle>Details</CardTitle>
          <CardDescription>The key is fixed: requests name the set by it.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[max-content_1fr]">
            <Setting label="Key">{assumptionSet.assumptionSetKey}</Setting>
            <Setting label="Display name">{assumptionSet.displayName}</Setting>
            <Setting label="Description">{assumptionSet.description || '—'}</Setting>
            <Setting label="Release">
              {assumptionSet.semanticReleaseId == null ? (
                'The active release, whichever is active'
              ) : (
                <span className="inline-flex items-center gap-2">
                  <Link className="font-medium hover:underline" to={`/t/${tenantId}/releases/${assumptionSet.semanticReleaseId}`}>
                    {release?.key}
                  </Link>
                  {release?.status && <StatusBadge status={release.status} />}
                </span>
              )}
            </Setting>
            <Setting label="Captured">{formatDateTime(assumptionSet.capturedAt)}</Setting>
            <Setting label="Created">{formatDateTime(assumptionSet.createdAt)}</Setting>
          </dl>
        </CardContent>
      </Card>
      <Card data-slot="assumption-set-pins">
        <CardHeader>
          <CardTitle>Pinned versions</CardTitle>
          <CardDescription>
            {isActive
              ? 'The published version of every dimension, mapping set and bridge.'
              : 'The versions published when the set was captured; later publishes retire them, but the set keeps them.'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <AssumptionPinsTable pins={pins} />
        </CardContent>
      </Card>
      <AssumptionSetSettingsSheet assumptionSetId={assumptionSet.assumptionSetId} {...labels.sheetProps} />
      <CaptureSheet {...capture.sheetProps} />
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${assumptionSet.assumptionSetKey}?`}
        description="The set and its pins are deleted, so requests can no longer name it. The versions it pins stay."
        confirmLabel="Delete assumption set"
        destructive
        onConfirm={() =>
          deleteSet.mutate(assumptionSet.assumptionSetId, { onSuccess: () => navigate(`/t/${tenantId}/assumption-sets`) })
        }
      />
    </div>
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
