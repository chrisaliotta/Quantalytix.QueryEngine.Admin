import { useState } from 'react';
import { Loading } from '@registry/components/Loading';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { DesignCoveragePanel } from './DesignCoveragePanel';
import { useReportDesign } from './designApi';
import { DesignRowTree } from './DesignRowTree';
import { DesignSourcesPanel } from './DesignSourcesPanel';
import { RowMappingTable } from './RowMappingTable';
import type { ReportDesign, ReportVersion } from './types';

export interface ReportDesignTabProps {
  version: ReportVersion;
  isEditable: boolean;
  /** Opens the advanced Sources tab. */
  onShowSources: () => void;
}

/**
 * The version's design, once it has loaded: its rows on the left, and on the right the source items mapped to the selected
 * row, the sources, and the items no row takes. The rows are the report's own dimension's version paired with this report
 * version, so a draft's rows change only the draft; its status changes move them together.
 */
export function ReportDesignTab({ version, isEditable, onShowSources }: ReportDesignTabProps) {
  const design = useReportDesign(version.reportVersionId);

  if (design.error) {
    return <ApiErrorAlert error={design.error} />;
  }
  if (!design.data) {
    return <Loading size="sm" />;
  }
  return <DesignEditor version={version} design={design.data} isEditable={isEditable} onShowSources={onShowSources} />;
}

interface DesignEditorProps extends ReportDesignTabProps {
  design: ReportDesign;
}

function DesignEditor({ version, design, isEditable, onShowSources }: DesignEditorProps) {
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const selected = design.rows.find(row => row.key === selectedKey) ?? null;

  return (
    <div data-slot="report-design-tab" className="grid items-start gap-4 lg:grid-cols-2">
      <DesignRowTree version={version} design={design} isEditable={isEditable} selectedKey={selectedKey} onSelect={setSelectedKey} />
      <Tabs defaultValue="mappings">
        <TabsList>
          <TabsTrigger value="mappings">Mappings</TabsTrigger>
          <TabsTrigger value="sources">Sources</TabsTrigger>
          <TabsTrigger value="coverage">Coverage</TabsTrigger>
        </TabsList>
        <TabsContent value="mappings">
          <RowMappingTable versionId={version.reportVersionId} design={design} row={selected} isEditable={isEditable} />
        </TabsContent>
        <TabsContent value="sources">
          <DesignSourcesPanel version={version} design={design} isEditable={isEditable} onShowSources={onShowSources} />
        </TabsContent>
        <TabsContent value="coverage">
          <DesignCoveragePanel versionId={version.reportVersionId} design={design} isEditable={isEditable} selectedKey={selectedKey} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
