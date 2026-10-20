import { BulkPasteDialog } from '@/shared/BulkPasteDialog';
import { useReplaceEntries } from './api';
import { ENTRY_LINE_FORMAT, readEntryLines, writeEntryLines } from './entryLines';
import type { MappingEntry } from './types';

export interface EntriesPasteDialogProps {
  versionId: number;
  /** The version's current entries, which the text starts with. */
  entries: MappingEntry[];
  onClose: () => void;
}

/** Replaces every entry of a draft or published mapping version from pasted `source_key,member_key` lines. */
export function EntriesPasteDialog({ versionId, entries, onClose }: EntriesPasteDialogProps) {
  const replace = useReplaceEntries(versionId);
  return (
    <BulkPasteDialog
      title="Bulk paste entries"
      format={ENTRY_LINE_FORMAT}
      initialText={writeEntryLines(entries)}
      read={readEntryLines}
      replace={replace.mutateAsync}
      listName="entries"
      noun={['entry', 'entries']}
      onClose={onClose}
    />
  );
}
