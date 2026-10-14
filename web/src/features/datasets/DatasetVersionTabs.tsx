import { Loading } from '@registry/components/Loading';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { BindingsTab } from '@/features/bindings/BindingsTab';
import { MeasuresTab } from '@/features/measures/MeasuresTab';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { useTenantId } from '@/shared/useTenantId';
import { FieldsTab } from './FieldsTab';
import { GrainsTab } from './GrainsTab';
import { useDatasetVersion } from './queries';
import { SettingsTab } from './SettingsTab';

export interface DatasetVersionTabsProps {
  versionId: number;
  /** Whether the version is a draft; every tab is read-only otherwise. */
  isDraft: boolean;
  /** The open tab, kept by the page so it survives switching versions. */
  tab: string;
  onTabChange: (tab: string) => void;
}

/** Editor of one dataset version: its fields, grains, bindings, measures and settings. */
export function DatasetVersionTabs({ versionId, isDraft, tab, onTabChange }: DatasetVersionTabsProps) {
  const tenantId = useTenantId();
  const version = useDatasetVersion(versionId);
  if (version.isPending) {
    return <Loading size="sm" />;
  }
  if (version.error) {
    return <ApiErrorAlert error={version.error} />;
  }

  return (
    <Tabs value={tab} onValueChange={onTabChange}>
      <TabsList>
        <TabsTrigger value="fields">Fields</TabsTrigger>
        <TabsTrigger value="grains">Grains</TabsTrigger>
        <TabsTrigger value="bindings">Bindings</TabsTrigger>
        <TabsTrigger value="measures">Measures</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="fields">
        <FieldsTab version={version.data} isDraft={isDraft} />
      </TabsContent>
      <TabsContent value="grains">
        <GrainsTab versionId={versionId} isDraft={isDraft} />
      </TabsContent>
      <TabsContent value="bindings">
        <BindingsTab tenantId={tenantId} datasetVersionId={versionId} isDraft={isDraft} />
      </TabsContent>
      <TabsContent value="measures">
        <MeasuresTab tenantId={tenantId} datasetVersionId={versionId} isDraft={isDraft} />
      </TabsContent>
      <TabsContent value="settings">
        <SettingsTab key={versionId} version={version.data} isDraft={isDraft} />
      </TabsContent>
    </Tabs>
  );
}
