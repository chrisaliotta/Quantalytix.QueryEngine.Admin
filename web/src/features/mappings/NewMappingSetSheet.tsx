import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, TextField } from '@/shared/form';
import { useCreateMappingSet } from './api';
import type { MappingSet } from './types';

interface NewMappingSetValues {
  mappingSetKey: string;
  displayName: string;
  sourceNamespace: string;
  dimensionId: number | null;
}

export interface NewMappingSetSheetProps {
  onClose: () => void;
  onCreated: (mappingSet: MappingSet) => void;
}

/** Creates a mapping set: its key and dimension are fixed once created, and it starts without versions. */
export function NewMappingSetSheet({ onClose, onCreated }: NewMappingSetSheetProps) {
  const create = useCreateMappingSet();
  const form = useForm<NewMappingSetValues>({
    defaultValues: { mappingSetKey: '', displayName: '', sourceNamespace: '', dimensionId: null },
  });

  async function submit(values: NewMappingSetValues) {
    onCreated(await create.mutateAsync({ ...values, dimensionId: values.dimensionId ?? 0 }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New mapping set"
      description="The key and dimension are fixed once created. Each version pins a version of the dimension, so the set starts without one."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="mappingSetKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <TextField
        name="sourceNamespace"
        label="Source namespace"
        description="The source system whose codes the set maps, such as core_banking."
      />
      <EntityField
        name="dimensionId"
        label="Dimension"
        lookupKind="dimensions"
        description="The canonical dimension the source codes map onto."
      />
    </EntityFormSheet>
  );
}
