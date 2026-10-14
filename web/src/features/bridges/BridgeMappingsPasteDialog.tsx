import { BulkPasteDialog } from '@/features/mappings/BulkPasteDialog';
import type { LookupItem } from '@/shared/types';
import { useReplaceBridgeMappings } from './api';
import { MAPPING_LINE_FORMAT, readMappingLines, writeMappingLines } from './mappingLines';
import type { BridgeMapping } from './types';

export interface BridgeMappingsPasteDialogProps {
  versionId: number;
  /** The draft's current mappings, which the text starts with. */
  mappings: BridgeMapping[];
  /** The members of the source version, which resolve the source keys. */
  sourceMembers: LookupItem[];
  /** The members of the target version, which resolve the target keys. */
  targetMembers: LookupItem[];
  onClose: () => void;
}

/** Replaces every mapping of a draft bridge version from pasted `source_key,target_key,factor,weight` lines. */
export function BridgeMappingsPasteDialog({
  versionId,
  mappings,
  sourceMembers,
  targetMembers,
  onClose,
}: BridgeMappingsPasteDialogProps) {
  const replace = useReplaceBridgeMappings(versionId);
  return (
    <BulkPasteDialog
      title="Bulk paste mappings"
      format={MAPPING_LINE_FORMAT}
      initialText={writeMappingLines(mappings, sourceMembers, targetMembers)}
      read={text => readMappingLines(text, sourceMembers, targetMembers)}
      replace={replace.mutateAsync}
      listName="mappings"
      noun={['mapping', 'mappings']}
      onClose={onClose}
    />
  );
}
