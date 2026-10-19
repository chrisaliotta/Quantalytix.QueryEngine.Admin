import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { useModelVersion } from '@/features/calculation-models/api';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { DroppedOnCloneAlert } from '@/shared/DroppedOnCloneAlert';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { useDeleteReport, useDeleteReportVersion, useReport, useReportVersion } from './api';
import { NewReportVersionDialog, type NewReportVersionValues } from './NewReportVersionDialog';
import { ReportIdentitySheet } from './ReportIdentitySheet';
import { ReportVersionPanel } from './ReportVersionPanel';
import type { CreatedReportVersion, Report } from './types';

/** The settings an empty draft starts with. */
const DEFAULT_SETTINGS = '{"periodicity":"month","timeZone":"UTC"}';

/** One report: identity, version picker with lifecycle actions, and the selected version's settings, lines and columns. */
export function ReportDetailPage() {
  const reportId = Number(useParams().reportId);
  const report = useReport(reportId);

  if (report.isPending) {
    return <Loading />;
  }
  if (report.error) {
    return <ApiErrorAlert error={report.error} />;
  }
  return <ReportDetail report={report.data} />;
}

function ReportDetail({ report }: { report: Report }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('report-versions', report.reportId);
  const deleteReport = useDeleteReport();
  const deleteVersion = useDeleteReportVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewReportVersionValues | null>(null);
  const [dropped, setDropped] = useState<CreatedReportVersion | null>(null);

  const versionItems = versions.data ?? [];
  const selected = selectedVersion(versionItems, searchParams.get('version'));
  const version = useReportVersion(selected?.id ?? null);
  const modelVersion = useModelVersion(version.data?.modelVersionId ?? null);

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) }, { replace: true });
  }

  function openNew() {
    setNewVersion({
      reportDimensionId: version.data?.reportDimensionId ?? null,
      modelId: modelVersion.data?.modelId ?? null,
      modelVersionId: null,
      defaultSourceProfileId: null,
      defaultSettingsJson: DEFAULT_SETTINGS,
      payloadJson: '',
      defaultFilterJson: '',
      defaultPivotJson: '',
      cloneFromVersionId: null,
    });
  }

  function openClone(sourceId: number) {
    const source = version.data;
    if (source?.reportVersionId === sourceId) {
      setNewVersion({
        reportDimensionId: source.reportDimensionId,
        modelId: modelVersion.data?.modelId ?? null,
        modelVersionId: source.modelVersionId ?? null,
        defaultSourceProfileId: source.defaultSourceProfileId ?? null,
        defaultSettingsJson: source.defaultSettingsJson ?? '',
        payloadJson: source.payloadJson ?? '',
        defaultFilterJson: source.defaultFilterJson ?? '',
        defaultPivotJson: source.defaultPivotJson ?? '',
        cloneFromVersionId: sourceId,
      });
    }
  }

  function showCreated(created: CreatedReportVersion) {
    toast.success(`Created draft v${created.version.versionNumber}.`);
    selectVersion(created.version.reportVersionId);
    if (created.droppedColumns.length + created.droppedLines.length > 0) {
      setDropped(created);
    }
  }

  return (
    <div data-slot="report-detail" className="space-y-6">
      <PageHeader
        title={report.displayName}
        description={`${report.reportKey} · ${report.reportType}`}
        actions={
          <>
            <EnabledBadge enabled={report.isEnabled} />
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
      <ApiErrorAlert error={deleteReport.error} />

      <VersionHeader
        kind="report_version"
        versions={versionItems.map(item => ({
          ...item,
          label: item.id === report.currentReportVersionId ? `${item.label} · current` : item.label,
        }))}
        selectedVersionId={selected?.id ?? null}
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
          title={`v${dropped.version.versionNumber} left out ${dropped.droppedColumns.length} columns and ${dropped.droppedLines.length} lines`}
          description="The chosen model version has no metric with the key these columns return, or these formula lines evaluate:"
          items={[...dropped.droppedColumns.map(key => `column ${key}`), ...dropped.droppedLines.map(key => `line ${key}`)]}
          onDismiss={() => setDropped(null)}
        />
      )}
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version reads one calculation model version and holds the report's lines and columns.
        </p>
      )}

      {selected && version.data && (
        <ReportVersionPanel key={version.data.reportVersionId} version={version.data} isDraft={selected.status === 'draft'} />
      )}

      {editingIdentity && <ReportIdentitySheet report={report} onClose={() => setEditingIdentity(false)} />}
      {newVersion && (
        <NewReportVersionDialog report={report} defaults={newVersion} onClose={() => setNewVersion(null)} onCreated={showCreated} />
      )}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${report.reportKey}?`}
        description="The report and its draft versions are deleted. A report with validated, published or retired versions, or one a release pins, is kept."
        confirmLabel="Delete report"
        destructive
        onConfirm={() => deleteReport.mutate(report.reportId, { onSuccess: () => navigate(`/t/${tenantId}/reports`) })}
      />
    </div>
  );
}
