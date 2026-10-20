import { useForm, useWatch } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useCreateDimension } from './api';
import type { Dimension } from './types';

interface NewDimensionValues {
  dimensionKey: string;
  displayName: string;
  dimensionKind: string | null;
  rangeDataType: string | null;
  isEnabled: boolean;
  isRemovable: boolean;
}

const EMPTY: NewDimensionValues = {
  dimensionKey: '',
  displayName: '',
  dimensionKind: null,
  rangeDataType: null,
  isEnabled: true,
  isRemovable: true,
};

export interface NewDimensionSheetProps {
  onClose: () => void;
  onCreated: (dimension: Dimension) => void;
}

/**
 * Creates a user-defined dimension with its draft version 1. A range data type is asked for only when the kind is
 * `range`.
 */
export function NewDimensionSheet({ onClose, onCreated }: NewDimensionSheetProps) {
  const create = useCreateDimension();
  const form = useForm<NewDimensionValues>({ defaultValues: EMPTY });
  const kind = useWatch({ control: form.control, name: 'dimensionKind' });

  async function submit(values: NewDimensionValues) {
    const dimension = await create.mutateAsync({
      ...values,
      dimensionKind: values.dimensionKind ?? '',
      rangeDataType: values.dimensionKind === 'range' ? values.rangeDataType : null,
    });
    onCreated(dimension);
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
      <SwitchField name="isEnabled" label="Enabled" description="Whether queries may use the dimension." />
      <SwitchField name="isRemovable" label="Removable" description="Whether the dimension may be deleted later." />
    </EntityFormSheet>
  );
}
