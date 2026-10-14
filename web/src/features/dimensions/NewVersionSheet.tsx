import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { DateTimeField, VocabularyField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useCreateDimensionVersion } from './api';
import type { DimensionVersion } from './types';

interface NewVersionValues {
  rangeDataType: string | null;
  validFrom: string | null;
  validTo: string | null;
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
 * Creates a draft version with its validity window: empty, or a clone of `source` (its levels, members, bands and
 * mapping versions). A clone keeps the source's range data type unless another one is chosen.
 */
export function NewVersionSheet({ dimensionId, isRange, source, rangeDataType, onClose, onCreated }: NewVersionSheetProps) {
  const create = useCreateDimensionVersion(dimensionId);
  const form = useForm<NewVersionValues>({
    defaultValues: { rangeDataType: source ? null : rangeDataType, validFrom: null, validTo: null },
  });

  async function submit(values: NewVersionValues) {
    onCreated(await create.mutateAsync({ ...values, cloneFromVersionId: source?.id ?? null }));
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={source ? `Clone ${source.key}` : 'New version'}
      description={describeDraft(source)}
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
      <DateTimeField
        name="validFrom"
        label="Valid from"
        description="Inclusive start; publishing closes the open-ended published version that starts earlier."
      />
      <DateTimeField name="validTo" label="Valid to" description="Exclusive end; leave empty for open-ended." />
    </EntityFormSheet>
  );
}

function describeDraft(source: LookupItem | null) {
  if (source) {
    return 'The draft copies the levels, members, range bands and pinned mapping versions of the source.';
  }
  return 'The draft starts empty.';
}
