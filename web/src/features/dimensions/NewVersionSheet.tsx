import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { VocabularyField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useCreateDimensionVersion } from './api';
import type { DimensionVersion } from './types';

interface NewVersionValues {
  rangeDataType: string | null;
}

export interface NewVersionSheetProps {
  dimensionId: number;
  isRange: boolean;
  /** The version to clone, or null for an empty draft. */
  source: LookupItem | null;
  /** The range data type an empty draft starts with, such as the latest version's. */
  rangeDataType: string | null;
  onClose: () => void;
  onCreated: (version: DimensionVersion) => void;
}

/**
 * Creates a draft version: empty, or a clone of `source` (its levels, members and bands). A clone keeps the source's range
 * data type unless another one is chosen. Publishing the draft later makes it the version every request reads.
 */
export function NewVersionSheet({ dimensionId, isRange, source, rangeDataType, onClose, onCreated }: NewVersionSheetProps) {
  const create = useCreateDimensionVersion(dimensionId);
  const form = useForm<NewVersionValues>({ defaultValues: { rangeDataType: source ? null : rangeDataType } });

  async function submit(values: NewVersionValues) {
    onCreated(await create.mutateAsync({ ...values, cloneFromVersionId: source?.id ?? null }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={source ? `Clone ${source.key}` : 'New version'}
      description={source ? 'The draft copies the levels, members and range bands of the source.' : 'The draft starts empty.'}
      form={form}
      onSubmit={submit}
      submitLabel="Create draft"
    >
      {isRange && (
        <VocabularyField
          name="rangeDataType"
          label="Range data type"
          vocabulary="range_data_type"
          allowEmpty={source !== null}
          description={source ? "Leave empty to keep the source's." : undefined}
        />
      )}
    </EntityFormSheet>
  );
}
