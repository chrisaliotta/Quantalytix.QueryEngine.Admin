import type { UseFormReturn } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import { AssumptionSetSettingsFields } from './AssumptionSetSettingsFields';
import type { AssumptionSetCreateInput } from './types';
import { useCreateAssumptionSet } from './useAssumptionSets';

export interface NewAssumptionSetSheetProps {
  form: UseFormReturn<AssumptionSetCreateInput>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Sheet that creates a draft assumption set, then opens it. */
export function NewAssumptionSetSheet({ form, open, onOpenChange }: NewAssumptionSetSheetProps) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const create = useCreateAssumptionSet();

  async function submit(values: AssumptionSetCreateInput) {
    const created = await create.mutateAsync({ ...values, description: values.description || null });
    navigate(`/t/${tenantId}/assumption-sets/${created.assumptionSetId}`);
  }

  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="New assumption set"
      description="An assumption set pins exact dimension versions for reproducible reporting. It starts as a draft without pins."
      form={form}
      onSubmit={submit}
      submitLabel="Create assumption set"
    >
      <TextField name="assumptionSetKey" label="Key" description="A stable name such as FY2026_Q3_CLOSE; it cannot change later." />
      <AssumptionSetSettingsFields />
    </EntityFormSheet>
  );
}
