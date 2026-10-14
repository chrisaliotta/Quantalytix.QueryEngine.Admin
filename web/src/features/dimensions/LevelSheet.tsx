import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { NumberField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { useCreateLevel, useUpdateLevel } from './api';
import type { DimensionLevel } from './types';

interface LevelValues {
  levelKey: string;
  displayName: string;
  ordinal: number | null;
  periodicity: string | null;
  isLeafLevel: boolean;
}

export interface LevelSheetProps {
  versionId: number;
  /** The level to edit or view; null to create one. */
  level: DimensionLevel | null;
  /** The ordinal a new level starts with. */
  nextOrdinal: number;
  readOnly: boolean;
  onClose: () => void;
}

/** Creates, edits or shows a level: its key (fixed once created), label, ordinal, periodicity and leaf flag. */
export function LevelSheet({ versionId, level, nextOrdinal, readOnly, onClose }: LevelSheetProps) {
  const create = useCreateLevel(versionId);
  const update = useUpdateLevel(versionId);
  const form = useForm<LevelValues>({
    defaultValues: {
      levelKey: level?.levelKey ?? '',
      displayName: level?.displayName ?? '',
      ordinal: level?.ordinal ?? nextOrdinal,
      periodicity: level?.periodicity ?? null,
      isLeafLevel: level?.isLeafLevel ?? false,
    },
  });

  async function submit(values: LevelValues) {
    const body = {
      displayName: values.displayName,
      ordinal: values.ordinal ?? 0,
      periodicity: values.periodicity,
      isLeafLevel: values.isLeafLevel,
    };
    if (level) {
      await update.mutateAsync({ levelId: level.levelId, body });
      return;
    }
    await create.mutateAsync({ ...body, levelKey: values.levelKey });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={sheetTitle(level, readOnly)}
      description="Levels label the hierarchy for grouping; the tree itself comes from member parents."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField
        name="levelKey"
        label="Key"
        description="A letter, then letters, digits or underscores; fixed once created."
        disabled={level !== null}
      />
      <TextField name="displayName" label="Name" />
      <NumberField name="ordinal" label="Ordinal" description="Business order of the levels, not the tree depth." />
      <VocabularyField
        name="periodicity"
        label="Periodicity"
        vocabulary="periodicity"
        allowEmpty
        description="For time-oriented levels only."
      />
      <SwitchField name="isLeafLevel" label="Leaf level" description="Marks the business-declared leaf level." />
    </EntityFormSheet>
  );
}

function sheetTitle(level: DimensionLevel | null, readOnly: boolean) {
  if (!level) {
    return 'New level';
  }
  if (readOnly) {
    return `Level ${level.levelKey}`;
  }
  return `Edit ${level.levelKey}`;
}
