import { CircleAlert, Pencil, Settings2, ShieldCheck, Trash2 } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { BehaviorSwitch } from '@registry/components/BehaviorSwitch';
import { Loading } from '@registry/components/Loading';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { PageHeader } from '@/shared/PageHeader';
import { RuleViolationList } from '@/shared/RuleViolationList';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { useDeleteReport, useReportCheck, useSetReportEnabled } from './api';
import { CalculationDimensionsTab } from './CalculationDimensionsTab';
import { useReportEditor } from './editorApi';
import { ReportColumnsTab } from './ReportColumnsTab';
import { ReportIdentitySheet } from './ReportIdentitySheet';
import { ReportScenariosTab } from './ReportScenariosTab';
import { ReportSettingsSheet } from './ReportSettingsSheet';
import { ReportVariableScopesTab } from './ReportVariableScopesTab';
import { RowsAndMappingsTab } from './RowsAndMappingsTab';
import { SourceProfilesTab } from './SourceProfilesTab';
import { SourcesTab } from './SourcesTab';
import { StylesTab } from './StylesTab';
import type { EditorView } from './types';
import { VariablesTab } from './VariablesTab';

/** One report in its editor; `?source=` opens the Sources tab on that source, as a report bridge's page links to it. */
export function ReportDetailPage() {
  const reportId = Number(useParams().reportId);
  const editor = useReportEditor(reportId);

  if (editor.isPending) {
    return <Loading />;
  }
  if (editor.error) {
    return <ApiErrorAlert error={editor.error} />;
  }
  return <ReportEditor view={editor.data} />;
}

/**
 * A report's editor: its header, with the switch that enables it once it passes every report rule, and its tabs, in the order a
 * report is built: rows and what each source gives them, the sources, the variables each source supplies, the profiles that pick
 * between them, the calculation dimensions, scenarios, scopes and columns, and the styles its rows show. While the report is
 * enabled, a write that would break a rule is refused and names it.
 */
function ReportEditor({ view }: { view: EditorView }) {
  const report = view.report;
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const sourceParam = searchParams.get('source');
  const highlightedSourceId = sourceParam === null ? null : Number(sourceParam);
  const [tab, setTab] = useState(highlightedSourceId === null ? 'rows' : 'sources');
  const setEnabled = useSetReportEnabled(report.reportId);
  const check = useReportCheck(report.reportId);
  const deleteReport = useDeleteReport();
  const models = useLookup('calculation-models');
  const model = models.data?.find(item => item.id === report.calculationModelId);
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [editingSettings, setEditingSettings] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const violations = check.data?.violations ?? [];

  function toggleEnabled(isEnabled: boolean) {
    setEnabled.mutate(isEnabled, {
      onSuccess: () => toast.success(`${report.reportKey} is ${isEnabled ? 'enabled; the engine runs it' : 'disabled'}.`),
    });
  }

  function runCheck() {
    check.mutate(undefined, {
      onSuccess: result => {
        if (result.violations.length === 0) {
          toast.success(`${report.reportKey} passes every report rule.`);
        }
      },
    });
  }

  return (
    <div data-slot="report-editor" className="space-y-6">
      <PageHeader
        title={report.displayName}
        description={`${report.reportKey} · ${report.reportType}`}
        actions={
          <>
            <BehaviorSwitch
              label="Enabled"
              description="The engine runs enabled reports only; enabling checks every report rule first."
              checked={report.isEnabled}
              disabled={setEnabled.isPending}
              onCheckedChange={toggleEnabled}
            />
            <Button variant="outline" disabled={check.isPending} onClick={runCheck}>
              <ShieldCheck />
              Check
            </Button>
            <Button variant="outline" onClick={() => setEditingSettings(true)}>
              <Settings2 />
              Settings
            </Button>
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
      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Setting label="Calculation model">
          <Link className="font-medium hover:underline" to={`/t/${tenantId}/calculation-models/${report.calculationModelId}`}>
            {model?.key} ({model?.classifier})
          </Link>
        </Setting>
        <Setting label="Default profile">{report.defaultProfileKey ?? 'None'}</Setting>
        <Setting label="Subtotals">{report.subtotalPosition}</Setting>
        <Setting label="Default settings">
          <code className="text-xs">{report.defaultSettingsJson ?? 'None'}</code>
        </Setting>
        <Setting label="Default pivot">
          <code className="text-xs">{report.defaultPivotJson ?? 'None'}</code>
        </Setting>
      </div>
      <ApiErrorAlert error={setEnabled.error ?? check.error ?? deleteReport.error} />
      {violations.length > 0 && (
        <Alert variant="destructive" data-slot="report-check-violations">
          <CircleAlert />
          <AlertTitle>{report.reportKey} breaks report rules</AlertTitle>
          <AlertDescription>
            <p>{report.isEnabled ? 'The engine runs it, so fix these first.' : 'Fix these before enabling it.'}</p>
            <RuleViolationList violations={violations} />
          </AlertDescription>
        </Alert>
      )}
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="rows">Rows &amp; mappings</TabsTrigger>
          <TabsTrigger value="sources">Sources</TabsTrigger>
          <TabsTrigger value="variables">Variables</TabsTrigger>
          <TabsTrigger value="profiles">Profiles</TabsTrigger>
          <TabsTrigger value="dimensions">Dimensions</TabsTrigger>
          <TabsTrigger value="scenarios">Scenarios</TabsTrigger>
          <TabsTrigger value="scopes">Scope</TabsTrigger>
          <TabsTrigger value="columns">Columns</TabsTrigger>
          <TabsTrigger value="styles">Styles</TabsTrigger>
        </TabsList>
        <TabsContent value="rows">
          <RowsAndMappingsTab view={view} />
        </TabsContent>
        <TabsContent value="sources">
          <SourcesTab view={view} highlightedSourceId={highlightedSourceId} />
        </TabsContent>
        <TabsContent value="variables">
          <VariablesTab view={view} onShowProfiles={() => setTab('profiles')} />
        </TabsContent>
        <TabsContent value="profiles">
          <SourceProfilesTab view={view} />
        </TabsContent>
        <TabsContent value="dimensions">
          <CalculationDimensionsTab reportId={report.reportId} />
        </TabsContent>
        <TabsContent value="scenarios">
          <ReportScenariosTab reportId={report.reportId} />
        </TabsContent>
        <TabsContent value="scopes">
          <ReportVariableScopesTab view={view} />
        </TabsContent>
        <TabsContent value="columns">
          <ReportColumnsTab report={report} />
        </TabsContent>
        <TabsContent value="styles">
          <StylesTab reportId={report.reportId} />
        </TabsContent>
      </Tabs>
      {editingIdentity && <ReportIdentitySheet report={report} onClose={() => setEditingIdentity(false)} />}
      {editingSettings && <ReportSettingsSheet report={report} onClose={() => setEditingSettings(false)} />}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${report.reportKey}?`}
        description="The report, its rows, its sources with their bridges, and everything else it owns are deleted."
        confirmLabel="Delete report"
        destructive
        onConfirm={() => deleteReport.mutate(report.reportId, { onSuccess: () => navigate(`/t/${tenantId}/reports`) })}
      />
    </div>
  );
}

/** One setting of the report: its label above its value. */
function Setting({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <div className="text-muted-foreground">{label}</div>
      <div className="font-medium">{children}</div>
    </div>
  );
}
