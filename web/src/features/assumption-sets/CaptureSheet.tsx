import type { UseFormReturn } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { AssumptionSetSettingsFields } from './AssumptionSetSettingsFields';
import type { AssumptionCaptureInput } from './types';
import { useCaptureAssumptionSet } from './useAssumptionSets';

export interface CaptureSheetProps {
  form: UseFormReturn<AssumptionCaptureInput>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Sheet that captures the active set as a historical set, then opens it. The capture pins exactly the versions published
 * now and the release active now; later publishes and activations leave the capture on them, so a request that names its key
 * reads those versions over that release's datasets.
 */
export function CaptureSheet({ form, open, onOpenChange }: CaptureSheetProps) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const capture = useCaptureAssumptionSet();

  async function submit(values: AssumptionCaptureInput) {
    const captured = await capture.mutateAsync({ ...values, description: values.description || null });
    navigate(`/t/${tenantId}/assumption-sets/${captured.assumptionSetId}`);
  }

  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="Capture the active set"
      description="Snapshots the versions published now, with the active release whose datasets they run over, under a key that requests can name to backtest against them."
      form={form}
      onSubmit={submit}
      submitLabel="Capture"
    >
      <TextField
        name="assumptionSetKey"
        label="Key"
        description="The name requests use, such as fy2026_q3_close; it cannot change later."
      />
      <AssumptionSetSettingsFields />
    </EntityFormSheet>
  );
}
