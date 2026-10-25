import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { useCreateMappingSet } from './api';
import type { MappingSet } from './types';

interface NewMappingSetValues {
  mappingSetKey: string;
  displayName: string;
  sourceNamespace: string;
}

export interface NewMappingSetSheetProps {
  /** The dimension whose page opened the sheet, onto whose members the set maps. */
  dimensionId: number;
  onClose: () => void;
  onCreated: (mappingSet: MappingSet) => void;
}

/** Creates a mapping set onto the dimension: its key is fixed once created, and it starts without versions. */
export function NewMappingSetSheet({ dimensionId, onClose, onCreated }: NewMappingSetSheetProps) {
  const create = useCreateMappingSet();
  const form = useForm<NewMappingSetValues>({
    defaultValues: { mappingSetKey: '', displayName: '', sourceNamespace: '' },
  });

  async function submit(values: NewMappingSetValues) {
    onCreated(await create.mutateAsync({ ...values, dimensionId }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New mapping set"
      description="The key is fixed once created. Each version pins a version of the dimension, so the set starts without one."
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
    </EntityFormSheet>
  );
}
