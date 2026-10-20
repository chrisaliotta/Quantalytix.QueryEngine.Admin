import { useForm } from 'react-hook-form';
import { FormDialog } from '@/shared/FormDialog';
import type { LookupItem } from '@/shared/types';
import { useCreateMappingVersion } from './api';
import type { MappingSet, MappingVersion } from './types';

export interface NewMappingVersionDialogProps {
  mappingSet: MappingSet;
  /** The version to clone, or null for an empty draft. */
  source: LookupItem | null;
  onClose: () => void;
  onCreated: (version: MappingVersion) => void;
}

/** Creates a draft of the set: empty, or a copy of the entries of `source`. */
export function NewMappingVersionDialog({ mappingSet, source, onClose, onCreated }: NewMappingVersionDialogProps) {
  const create = useCreateMappingVersion(mappingSet.mappingSetId);
  const form = useForm();

  async function submit() {
    onCreated(await create.mutateAsync({ cloneFromVersionId: source?.id ?? null }));
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title={source ? `Clone ${source.key}` : 'New version'}
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      <p className="text-sm text-muted-foreground">
        {source ? `The draft copies the entries of ${source.key}.` : 'The draft starts without entries.'} Publishing it
        replaces the published version, which requests read by default.
      </p>
    </FormDialog>
  );
}
