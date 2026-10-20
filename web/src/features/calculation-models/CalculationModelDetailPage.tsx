import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { toast } from 'sonner';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import type { VersionStatus } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { VersionHeader } from '@/shared/VersionHeader';
import { selectedVersion } from '@/shared/versionSelection';
import { isEditable as isEditableVersion } from '@/shared/versionTransitions';
import { useCalculationModel, useDeleteCalculationModel, useDeleteModelVersion, useModelVersion } from './api';
import { CalculatedMeasuresTab } from './CalculatedMeasuresTab';
import { CalculationDimensionsTab } from './CalculationDimensionsTab';
import { CalculationModelIdentitySheet } from './CalculationModelIdentitySheet';
import { CalculationRoutesTab } from './CalculationRoutesTab';
import { ModelVersionSheet } from './ModelVersionSheet';
import { NewModelVersionDialog } from './NewModelVersionDialog';
import { SourceProfilesTab } from './SourceProfilesTab';
import type { CalculationModel, CalculationModelVersion, NewModelVersionInput } from './types';
import { VariableSourcesTab } from './VariableSourcesTab';
import { VariablesTab } from './VariablesTab';

/** One calculation model: identity, version picker with lifecycle actions, and the selected version's content in tabs. */
export function CalculationModelDetailPage() {
  const modelId = Number(useParams().modelId);
  const model = useCalculationModel(modelId);

  if (model.isPending) {
    return <Loading />;
  }
  if (model.error) {
    return <ApiErrorAlert error={model.error} />;
  }
  return <CalculationModelDetail model={model.data} />;
}

function CalculationModelDetail({ model }: { model: CalculationModel }) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const versions = useLookup('model-versions', model.modelId);
  const deleteModel = useDeleteCalculationModel();
  const deleteVersion = useDeleteModelVersion();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [editingVersion, setEditingVersion] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [newVersion, setNewVersion] = useState<NewModelVersionInput | null>(null);

  const versionItems = versions.data ?? [];
  const selected = selectedVersion(versionItems, searchParams.get('version'));
  const version = useModelVersion(selected?.id ?? null);
  const isEditable = isEditableVersion('calculation_model_version', selected?.status as VersionStatus | undefined);

  function selectVersion(id: number) {
    setSearchParams({ version: String(id) }, { replace: true });
  }

  function showCreated(created: CalculationModelVersion) {
    toast.success(`Created draft v${created.versionNumber}.`);
    selectVersion(created.modelVersionId);
  }

  return (
    <div data-slot="calculation-model-detail" className="space-y-6">
      <PageHeader
        title={model.displayName}
        description={model.modelKey}
        actions={
          <>
            <EnabledBadge enabled={model.isEnabled} />
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
      <ApiErrorAlert error={deleteModel.error} />

      <VersionHeader
        kind="calculation_model_version"
        versions={versionItems}
        selectedVersionId={selected?.id ?? null}
        onSelectVersion={selectVersion}
        onCreateVersion={() => setNewVersion({ currencyCode: version.data?.currencyCode ?? 'USD', cloneFromVersionId: null })}
        onCloneVersion={sourceId => setNewVersion({ currencyCode: version.data?.currencyCode ?? 'USD', cloneFromVersionId: sourceId })}
        onDeleteVersion={draftId =>
          deleteVersion.mutate(draftId, { onSuccess: () => setSearchParams({}, { replace: true }) })
        }
      />
      <ApiErrorAlert error={versions.error ?? version.error ?? deleteVersion.error} />
      {versions.isSuccess && versionItems.length === 0 && (
        <p className="text-sm text-muted-foreground">
          No versions yet. A version holds the variables, their sources, profiles, formulas and calculation dimensions.
        </p>
      )}

      {selected && version.data && (
        <>
          <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
            <Setting label="Currency" value={version.data.currencyCode} />
            <Setting label="Calendar" value={version.data.calendarKey} />
            <Setting label="Formula language" value={version.data.formulaLanguageVersion} />
            {isEditable && (
              <Button variant="outline" size="sm" className="ml-auto" onClick={() => setEditingVersion(true)}>
                <Pencil />
                Edit settings
              </Button>
            )}
          </div>
          <Tabs defaultValue="variables">
            <TabsList>
              <TabsTrigger value="variables">Variables</TabsTrigger>
              <TabsTrigger value="sources">Sources</TabsTrigger>
              <TabsTrigger value="profiles">Profiles</TabsTrigger>
              <TabsTrigger value="calculated-measures">Calculated measures</TabsTrigger>
              <TabsTrigger value="dimensions">Dimensions</TabsTrigger>
              <TabsTrigger value="routes">Routes</TabsTrigger>
            </TabsList>
            <TabsContent value="variables">
              <VariablesTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
            <TabsContent value="sources">
              <VariableSourcesTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
            <TabsContent value="profiles">
              <SourceProfilesTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
            <TabsContent value="calculated-measures">
              <CalculatedMeasuresTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
            <TabsContent value="dimensions">
              <CalculationDimensionsTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
            <TabsContent value="routes">
              <CalculationRoutesTab key={selected.id} versionId={selected.id} isEditable={isEditable} />
            </TabsContent>
          </Tabs>
        </>
      )}

      {editingIdentity && <CalculationModelIdentitySheet model={model} onClose={() => setEditingIdentity(false)} />}
      {editingVersion && version.data && <ModelVersionSheet version={version.data} onClose={() => setEditingVersion(false)} />}
      {newVersion && (
        <NewModelVersionDialog model={model} defaults={newVersion} onClose={() => setNewVersion(null)} onCreated={showCreated} />
      )}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${model.modelKey}?`}
        description="The model and its draft versions are deleted. A model with validated, published or retired versions, or one a report version reads, is kept."
        confirmLabel="Delete model"
        destructive
        onConfirm={() =>
          deleteModel.mutate(model.modelId, { onSuccess: () => navigate(`/t/${tenantId}/calculation-models`) })
        }
      />
    </div>
  );
}

function Setting({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <div className="text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
    </div>
  );
}
