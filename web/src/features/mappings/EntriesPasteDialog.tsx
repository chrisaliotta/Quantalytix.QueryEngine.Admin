import { BulkPasteDialog } from '@/shared/BulkPasteDialog';
import type { LookupItem } from '@/shared/types';
import { useReplaceEntries } from './api';
import { ENTRY_LINE_FORMAT, readEntryLines, writeEntryLines } from './entryLines';
import type { MappingEntry } from './types';

export interface EntriesPasteDialogProps {
  versionId: number;
  /** The draft's current entries, which the text starts with. */
  entries: MappingEntry[];
  /** The members of the pinned dimension version, which resolve the member keys. */
  members: LookupItem[];
  onClose: () => void;
}

/** Replaces every entry of a draft mapping version from pasted `source_key,member_key` lines. */
export function EntriesPasteDialog({ versionId, entries, members, onClose }: EntriesPasteDialogProps) {
  const replace = useReplaceEntries(versionId);
  return (
    <BulkPasteDialog
      title="Bulk paste entries"
      format={ENTRY_LINE_FORMAT}
      initialText={writeEntryLines(entries, members)}
      read={text => readEntryLines(text, members)}
      replace={replace.mutateAsync}
      listName="entries"
      noun={['entry', 'entries']}
      onClose={onClose}
    />
  );
}
