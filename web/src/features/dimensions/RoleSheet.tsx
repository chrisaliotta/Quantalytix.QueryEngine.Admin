import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextAreaField, TextField } from '@/shared/form';
import { useCreateRole, useUpdateRole } from './api';
import type { DimensionRole } from './types';

interface RoleValues {
  dimensionRoleKey: string;
  displayName: string;
  description: string;
}

export interface RoleSheetProps {
  dimensionId: number;
  /** The role to edit; null to create one. */
  role: DimensionRole | null;
  onClose: () => void;
}

/** Creates or edits a role: its key, which requests name its bindings by and which is fixed once created, label and description. */
export function RoleSheet({ dimensionId, role, onClose }: RoleSheetProps) {
  const create = useCreateRole(dimensionId);
  const update = useUpdateRole(dimensionId);
  const form = useForm<RoleValues>({
    defaultValues: { dimensionRoleKey: role?.dimensionRoleKey ?? '', displayName: role?.displayName ?? '', description: role?.description ?? '' },
  });

  async function submit(values: RoleValues) {
    const body = { displayName: values.displayName, description: values.description || null };
    if (role) {
      await update.mutateAsync({ dimensionRoleId: role.dimensionRoleId, body });
      return;
    }
    await create.mutateAsync({ ...body, dimensionRoleKey: values.dimensionRoleKey });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={role ? `Edit ${role.dimensionRoleKey}` : 'New role'}
      description="A named use of the dimension, such as the servicing branch of a loan. The catalog shows its name and description."
      form={form}
      onSubmit={submit}
    >
      <TextField
        name="dimensionRoleKey"
        label="Key"
        description="A letter, then letters, digits or underscores; unique among every dimension's roles and fixed once created."
        disabled={role !== null}
      />
      <TextField name="displayName" label="Name" />
      <TextAreaField name="description" label="Description" />
    </EntityFormSheet>
  );
}
