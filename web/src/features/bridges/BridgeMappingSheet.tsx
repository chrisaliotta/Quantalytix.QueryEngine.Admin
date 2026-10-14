import { useForm } from 'react-hook-form';
import { problemOnItem } from '@/features/mappings/bulkLists';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, NumberField } from '@/shared/form';
import { useReplaceBridgeMappings } from './api';
import { withoutMapping, type BridgeMappingRow } from './mappingList';
import type { BridgeMapping, BridgeVersion } from './types';

interface BridgeMappingValues {
  sourceMemberId: number | null;
  targetMemberId: number | null;
  factor: number | null;
  allocationWeight: number | null;
}

/** What the allocation weight means in each allocation mode. */
const WEIGHT_DESCRIPTIONS: Record<string, string> = {
  none: 'Exactly 1: the source moves wholly to its one target.',
  partition: "0 or more; each source member's weights sum to exactly 1.",
  replicate: '0 or more; the source counts fully in every target.',
};

export interface BridgeMappingSheetProps {
  version: BridgeVersion;
  /** Every mapping of the version; saving replaces them with this list, edited. */
  mappings: BridgeMapping[];
  /** The mapping to edit or view; null to add one. */
  mapping: BridgeMappingRow | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Adds, edits or shows one mapping of a bridge version. The API replaces a version's mappings as a whole, so saving
 * sends the version's list with this mapping in place, and the problems of this mapping's values land on its fields.
 */
export function BridgeMappingSheet({ version, mappings, mapping, readOnly, onClose }: BridgeMappingSheetProps) {
  const replace = useReplaceBridgeMappings(version.bridgeVersionId);
  const form = useForm<BridgeMappingValues>({
    defaultValues: {
      sourceMemberId: mapping?.sourceMemberId ?? null,
      targetMemberId: mapping?.targetMemberId ?? null,
      factor: mapping?.factor ?? 1,
      allocationWeight: mapping?.allocationWeight ?? 1,
    },
  });

  async function submit(values: BridgeMappingValues) {
    const others = withoutMapping(mappings, mapping);
    const edited = {
      sourceMemberId: values.sourceMemberId ?? 0,
      targetMemberId: values.targetMemberId ?? 0,
      factor: values.factor ?? 0,
      allocationWeight: values.allocationWeight ?? 0,
    };
    try {
      await replace.mutateAsync([...others, edited]);
    } catch (error) {
      throw problemOnItem(error, 'mappings', others.length);
    }
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={sheetTitle(mapping, readOnly)}
      description="A mapping assigns a source member to a target member; each pair appears once per version."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <EntityField
        name="sourceMemberId"
        label="Source member"
        lookupKind="members"
        parentId={version.sourceDimensionVersionId}
        description="A member of the source version."
      />
      <EntityField
        name="targetMemberId"
        label="Target member"
        lookupKind="members"
        parentId={version.targetDimensionVersionId}
        description="A member of the target version."
      />
      <NumberField
        name="factor"
        label="Factor"
        description="Signed coefficient of the source's values, such as -1 for sign normalization."
      />
      <NumberField
        name="allocationWeight"
        label="Allocation weight"
        description={WEIGHT_DESCRIPTIONS[version.allocationMode]}
      />
    </EntityFormSheet>
  );
}

function sheetTitle(mapping: BridgeMappingRow | null, readOnly: boolean) {
  if (!mapping) {
    return 'New mapping';
  }
  const pair = `${mapping.sourceKey} → ${mapping.targetKey}`;
  if (readOnly) {
    return `Mapping ${pair}`;
  }
  return `Edit ${pair}`;
}
