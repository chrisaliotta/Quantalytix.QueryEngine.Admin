import { Pencil } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useModelVersion } from '@/features/calculation-models/api';
import { StatusBadge } from '@/shared/StatusBadge';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { ReportColumnsTab } from './ReportColumnsTab';
import { ReportLinesTab } from './ReportLinesTab';
import { ReportVersionSheet } from './ReportVersionSheet';
import type { ReportVersion } from './types';

export interface ReportVersionPanelProps {
  version: ReportVersion;
  isDraft: boolean;
}

/** The selected report version: what it reads and its defaults, editable on a draft, above its lines and columns. */
export function ReportVersionPanel({ version, isDraft }: ReportVersionPanelProps) {
  const tenantId = useTenantId();
  const [editing, setEditing] = useState(false);
  const dimensions = useLookup('dimensions');
  const modelVersion = useModelVersion(version.modelVersionId ?? null);
  const models = useLookup('calculation-models');
  const profiles = useLookup('source-profiles', version.modelVersionId ?? null);
  const dimensionKey = dimensions.data?.find(item => item.id === version.reportDimensionId)?.key ?? '';
  const modelKey = models.data?.find(item => item.id === modelVersion.data?.modelId)?.key ?? '';
  const profileKey = profiles.data?.find(item => item.id === version.defaultSourceProfileId)?.key ?? 'None';

  return (
    <div data-slot="report-version-panel" className="space-y-4">
      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Setting label="Report dimension" value={dimensionKey} />
        <div className="space-y-1">
          <div className="text-muted-foreground">Model version</div>
          {modelVersion.data && (
            <div className="flex items-center gap-2">
              <Link
                className="font-medium hover:underline"
                to={`/t/${tenantId}/calculation-models/${modelVersion.data.modelId}?version=${modelVersion.data.modelVersionId}`}
              >
                {modelKey} v{modelVersion.data.versionNumber}
              </Link>
              <StatusBadge status={modelVersion.data.status} />
            </div>
          )}
        </div>
        <Setting label="Default profile" value={profileKey} />
        <Setting label="Default settings" value={version.defaultSettingsJson ?? 'None'} code />
        <Setting label="Default pivot" value={version.defaultPivotJson ?? 'None'} code />
        {isDraft && (
          <Button variant="outline" size="sm" className="ml-auto" onClick={() => setEditing(true)}>
            <Pencil />
            Edit settings
          </Button>
        )}
      </div>
      <Tabs defaultValue="lines">
        <TabsList>
          <TabsTrigger value="lines">Lines</TabsTrigger>
          <TabsTrigger value="columns">Columns</TabsTrigger>
        </TabsList>
        <TabsContent value="lines">
          <ReportLinesTab version={version} isDraft={isDraft} />
        </TabsContent>
        <TabsContent value="columns">
          <ReportColumnsTab version={version} isDraft={isDraft} />
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
