import type { UseFormReturn } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, TextField } from '@/shared/form';
import { useTenantId } from '@/shared/useTenantId';
import type { ReleaseCreateInput } from './types';
import { useCreateRelease } from './useReleases';

export interface NewReleaseSheetProps {
  /** The sheet's form; open it with `useSheetForm` so it starts from the release to copy. */
  form: UseFormReturn<ReleaseCreateInput>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Sheet that creates a draft release, copying another release's manifest unless cleared, then opens the new release. */
export function NewReleaseSheet({ form, open, onOpenChange }: NewReleaseSheetProps) {
  const tenantId = useTenantId();
  const navigate = useNavigate();
  const create = useCreateRelease();

  async function submit(values: ReleaseCreateInput) {
    const release = await create.mutateAsync(values);
    navigate(`/t/${tenantId}/releases/${release.releaseId}`);
  }

  return (
    <EntityFormSheet
      open={open}
      onOpenChange={onOpenChange}
      title="New release"
      description="A new release starts as a draft. Its manifest pins one dataset version per dataset."
      form={form}
      onSubmit={submit}
      submitLabel="Create release"
    >
      <TextField name="releaseKey" label="Key" description="A stable label such as development/2026-q4." />
      <EntityField
        name="copyFromReleaseId"
        label="Copy manifest from"
        lookupKind="releases"
        allowEmpty
        description="Starts from this release's pinned dataset versions; clear it to start with an empty manifest."
      />
    </EntityFormSheet>
  );
}
