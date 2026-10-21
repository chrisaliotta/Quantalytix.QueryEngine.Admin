import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { Loading } from '@registry/components/Loading';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { ConfirmDialog } from '@/shared/ConfirmDialog';
import { EnabledBadge } from '@/shared/EnabledBadge';
import { PageHeader } from '@/shared/PageHeader';
import { useTenantId } from '@/shared/useTenantId';
import { useCalculationModel, useDeleteCalculationModel } from './api';
import { CalculatedMeasuresTab } from './CalculatedMeasuresTab';
import { CalculationModelIdentitySheet } from './CalculationModelIdentitySheet';
import type { CalculationModel } from './types';
import { VariablesTab } from './VariablesTab';

/** One calculation model: identity, settings, and its metrics in tabs, edited live. */
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
  const deleteModel = useDeleteCalculationModel();
  const [editingIdentity, setEditingIdentity] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);

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

      <div className="flex flex-wrap items-start gap-x-8 gap-y-3 rounded-md border p-3 text-sm">
        <Setting label="Currency" value={model.currencyCode} />
        <Setting label="Calendar" value={model.calendarKey} />
        <Setting label="Formula language" value={model.formulaLanguageVersion} />
      </div>
      <p className="text-sm text-muted-foreground">
        Edits are live: every enabled report over the model reads them at once, so an edit that would break a formula or such a
        report is refused. Each report brings its own data.
      </p>
      <Tabs defaultValue="variables">
        <TabsList>
          <TabsTrigger value="variables">Variables</TabsTrigger>
          <TabsTrigger value="calculated-measures">Calculated measures</TabsTrigger>
        </TabsList>
        <TabsContent value="variables">
          <VariablesTab modelId={model.modelId} />
        </TabsContent>
        <TabsContent value="calculated-measures">
          <CalculatedMeasuresTab modelId={model.modelId} />
        </TabsContent>
      </Tabs>

      {editingIdentity && <CalculationModelIdentitySheet model={model} onClose={() => setEditingIdentity(false)} />}
      <ConfirmDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title={`Delete ${model.modelKey}?`}
        description="The model is deleted with its variables and calculated measures. A model a report reads is kept."
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
