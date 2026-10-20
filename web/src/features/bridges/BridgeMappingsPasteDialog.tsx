import { BulkPasteDialog } from '@/shared/BulkPasteDialog';
import { useReplaceBridgeMappings } from './api';
import { MAPPING_LINE_FORMAT, readMappingLines, writeMappingLines } from './mappingLines';
import type { BridgeMapping } from './types';

export interface BridgeMappingsPasteDialogProps {
  versionId: number;
  /** The version's current mappings, which the text starts with. */
  mappings: BridgeMapping[];
  onClose: () => void;
}

/** Replaces every mapping of a draft or published bridge version from pasted `source_key,target_key,factor,weight` lines. */
export function BridgeMappingsPasteDialog({ versionId, mappings, onClose }: BridgeMappingsPasteDialogProps) {
  const replace = useReplaceBridgeMappings(versionId);
  return (
    <BulkPasteDialog
      title="Bulk paste mappings"
      format={MAPPING_LINE_FORMAT}
      initialText={writeMappingLines(mappings)}
      read={readMappingLines}
      replace={replace.mutateAsync}
      listName="mappings"
      noun={['mapping', 'mappings']}
      onClose={onClose}
    />
  );
}
