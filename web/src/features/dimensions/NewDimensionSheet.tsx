import { useEffect, useRef } from 'react';
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useCreateDimension } from './api';
import type { Dimension } from './types';

interface NewDimensionValues {
  dimensionKey: string;
  displayName: string;
  dimensionKind: string | null;
  rangeDataType: string | null;
  roleKey: string;
  roleDisplayName: string;
  isEnabled: boolean;
  isRemovable: boolean;
}

const EMPTY: NewDimensionValues = {
  dimensionKey: '',
  displayName: '',
  dimensionKind: null,
  rangeDataType: null,
  roleKey: '',
  roleDisplayName: '',
  isEnabled: true,
  isRemovable: true,
};

export interface NewDimensionSheetProps {
  onClose: () => void;
  onCreated: (dimension: Dimension) => void;
}

/**
 * Creates a user-defined dimension with its draft version 1 and, unless it is a time dimension, its first role, whose key
 * and name follow the dimension's until changed. A range data type is asked for only when the kind is `range`.
 */
export function NewDimensionSheet({ onClose, onCreated }: NewDimensionSheetProps) {
  const create = useCreateDimension();
  const form = useForm<NewDimensionValues>({ defaultValues: EMPTY });
  const kind = useWatch({ control: form.control, name: 'dimensionKind' });
  useRoleFollowsDimension(form);

  async function submit(values: NewDimensionValues) {
    const { roleKey, roleDisplayName, ...dimension } = values;
    const created = await create.mutateAsync({
      ...dimension,
      dimensionKind: values.dimensionKind ?? '',
      rangeDataType: values.dimensionKind === 'range' ? values.rangeDataType : null,
      defaultRole: values.dimensionKind === 'time' ? null : { roleKey, displayName: roleDisplayName, description: null },
    });
    onCreated(created);
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title="New dimension"
      description="The key and kind are fixed once created. Version 1 starts as a draft."
      form={form}
      onSubmit={submit}
      submitLabel="Create"
    >
      <TextField name="dimensionKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <VocabularyField name="dimensionKind" label="Kind" vocabulary="dimension_kind" />
      {kind === 'range' && (
        <VocabularyField
          name="rangeDataType"
          label="Range data type"
          vocabulary="range_data_type"
          description="The type of the values the bands compare."
        />
      )}
      {kind !== 'time' && (
        <>
          <TextField
            name="roleKey"
            label="Role key"
            description="The first role, which bindings bind and requests name; unique among every dimension's roles."
          />
          <TextField name="roleDisplayName" label="Role name" />
        </>
      )}
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the dimension." />
      <SwitchField name="isRemovable" label="Removable" description="Whether the dimension may be deleted later." />
    </EntityFormSheet>
  );
}

/** Copies the dimension's key and name into the role's while the role's still equal what the dimension's were. */
function useRoleFollowsDimension(form: UseFormReturn<NewDimensionValues>) {
  const dimensionKey = useWatch({ control: form.control, name: 'dimensionKey' });
  const displayName = useWatch({ control: form.control, name: 'displayName' });
  const previous = useRef({ dimensionKey, displayName });
  useEffect(() => {
    const before = previous.current;
    previous.current = { dimensionKey, displayName };
    if (form.getValues('roleKey') === before.dimensionKey) {
      form.setValue('roleKey', dimensionKey);
    }
    if (form.getValues('roleDisplayName') === before.displayName) {
      form.setValue('roleDisplayName', displayName);
    }
  }, [form, dimensionKey, displayName]);
}
