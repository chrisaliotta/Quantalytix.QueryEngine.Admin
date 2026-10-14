import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField } from '@/shared/form';
import { useUpdateMappingSet } from './api';
import type { MappingSet, MappingSetInput } from './types';

export interface MappingSetIdentitySheetProps {
  mappingSet: MappingSet;
  onClose: () => void;
}

/** Edits a mapping set's name and source namespace; its key and dimension are fixed. */
export function MappingSetIdentitySheet({ mappingSet, onClose }: MappingSetIdentitySheetProps) {
  const update = useUpdateMappingSet(mappingSet.mappingSetId);
  const form = useForm<MappingSetInput>({
    defaultValues: { displayName: mappingSet.displayName, sourceNamespace: mappingSet.sourceNamespace },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit ${mappingSet.mappingSetKey}`}
      description="A change refreshes the query engine's cached metadata."
      form={form}
      onSubmit={values => update.mutateAsync(values)}
    >
      <TextField name="displayName" label="Name" />
      <TextField
        name="sourceNamespace"
        label="Source namespace"
        description="The source system whose codes the set maps, such as core_banking."
      />
    </EntityFormSheet>
  );
}
