import { Pencil } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { usePublishedVersion } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { CalculationDimensionsTab } from './CalculationDimensionsTab';
import { CalculationRoutesTab } from './CalculationRoutesTab';
import { ReportColumnsTab } from './ReportColumnsTab';
import { ReportRowsTab } from './ReportRowsTab';
import { ReportVariableTargetsTab } from './ReportVariableTargetsTab';
import { ReportVersionSheet } from './ReportVersionSheet';
import { SourceProfilesTab } from './SourceProfilesTab';
import type { Report, ReportVersion } from './types';
import { VariableSourcesTab } from './VariableSourcesTab';

export interface ReportVersionPanelProps {
  report: Report;
  version: ReportVersion;
  isEditable: boolean;
}

/**
 * The selected report version: the calculation model it reads, its defaults, editable on a draft or the published version,
 * above its sources, profiles, calculation dimensions, routes, targets, columns and rows. Requests read the model version
 * their assumption set pins, by default its published one, which the panel links to.
 */
export function ReportVersionPanel({ report, version, isEditable }: ReportVersionPanelProps) {
  const tenantId = useTenantId();
  const [editing, setEditing] = useState(false);
  const dimensions = useLookup('dimensions');
  const models = useLookup('calculation-models');
  const publishedModel = usePublishedVersion('model-versions', version.modelId);
  const dimensionKey = dimensions.data?.find(item => item.id === report.reportDimensionId)?.key ?? '';
  const modelKey = models.data?.find(item => item.id === version.modelId)?.key ?? '';
  const versionId = version.reportVersionId;

  return (
    <div data-slot="report-version-panel" className="space-y-4">
      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Setting label="Report dimension" value={dimensionKey} />
        <div className="space-y-1">
          <div className="text-muted-foreground">Calculation model</div>
          <Link
            className="font-medium hover:underline"
            to={`/t/${tenantId}/calculation-models/${version.modelId}${publishedModel ? `?version=${publishedModel.id}` : ''}`}
          >
            {modelKey} {publishedModel ? `(published ${publishedModel.key})` : '(no published version)'}
          </Link>
        </div>
        <Setting label="Default profile" value={version.defaultProfileKey ?? 'None'} />
        <Setting label="Subtotals" value={version.subtotalPosition} />
        <Setting label="Default settings" value={version.defaultSettingsJson ?? 'None'} code />
        <Setting label="Default pivot" value={version.defaultPivotJson ?? 'None'} code />
        {isEditable && (
          <Button variant="outline" size="sm" className="ml-auto" onClick={() => setEditing(true)}>
            <Pencil />
            Edit settings
          </Button>
        )}
      </div>
      <Tabs defaultValue="sources">
        <TabsList>
          <TabsTrigger value="sources">Sources</TabsTrigger>
          <TabsTrigger value="profiles">Profiles</TabsTrigger>
          <TabsTrigger value="dimensions">Dimensions</TabsTrigger>
          <TabsTrigger value="routes">Routes</TabsTrigger>
          <TabsTrigger value="targets">Targets</TabsTrigger>
          <TabsTrigger value="columns">Columns</TabsTrigger>
          <TabsTrigger value="rows">Rows</TabsTrigger>
        </TabsList>
        <TabsContent value="sources">
          <VariableSourcesTab report={report} version={version} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="profiles">
          <SourceProfilesTab version={version} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="dimensions">
          <CalculationDimensionsTab versionId={versionId} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="routes">
          <CalculationRoutesTab versionId={versionId} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="targets">
          <ReportVariableTargetsTab report={report} version={version} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="columns">
          <ReportColumnsTab version={version} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="rows">
          <ReportRowsTab report={report} version={version} isEditable={isEditable} />
        </TabsContent>
      </Tabs>
      {editing && <ReportVersionSheet version={version} onClose={() => setEditing(false)} />}
    </div>
  );
}

function Setting({ label, value, code = false }: { label: string; value: string; code?: boolean }) {
  return (
    <div className="space-y-1">
      <div className="text-muted-foreground">{label}</div>
      {code ? <code className="text-xs">{value}</code> : <div className="font-medium">{value}</div>}
    </div>
  );
}
