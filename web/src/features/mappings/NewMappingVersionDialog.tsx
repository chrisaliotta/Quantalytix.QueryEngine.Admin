import { useForm } from 'react-hook-form';
import { EntityField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import type { LookupItem } from '@/shared/types';
import { useCreateMappingVersion } from './api';
import type { CreatedMappingVersion, MappingSet } from './types';

interface NewMappingVersionValues {
  dimensionVersionId: number | null;
}

export interface NewMappingVersionDialogProps {
  mappingSet: MappingSet;
  /** The version to clone, or null for an empty draft. */
  source: LookupItem | null;
  /** The dimension version the picker starts with, such as the latest published one. */
  dimensionVersionId: number | null;
  onClose: () => void;
  onCreated: (created: CreatedMappingVersion) => void;
}

/**
 * Creates a draft pinned to a version of the set's dimension: empty, or a clone of `source` whose entries move to the
 * members with the same keys in the pinned version.
 */
export function NewMappingVersionDialog({
  mappingSet,
  source,
  dimensionVersionId,
  onClose,
  onCreated,
}: NewMappingVersionDialogProps) {
  const create = useCreateMappingVersion(mappingSet.mappingSetId);
  const form = useForm<NewMappingVersionValues>({ defaultValues: { dimensionVersionId } });

  async function submit(values: NewMappingVersionValues) {
    const created = await create.mutateAsync({
      dimensionVersionId: values.dimensionVersionId ?? 0,
      cloneFromVersionId: source?.id ?? null,
    });
    onCreated(created);
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title={source ? `Clone ${source.key}` : 'New version'}
      description={describeDraft(source)}
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <EntityField
        name="dimensionVersionId"
        label="Dimension version"
        lookupKind="dimension-versions"
        parentId={mappingSet.dimensionId}
        description="The version whose members the entries map to. Validation needs it published and pinned by no other version of the set."
      />
    </FormDialog>
  );
}

function describeDraft(source: LookupItem | null) {
  if (source) {
    return `The draft copies the entries of ${source.key}. Each moves to the member with the same key in the chosen version; entries whose key it lacks are left out.`;
  }
  return 'The draft starts without entries.';
}
