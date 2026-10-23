import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { KeyField, TextField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useCreateEntry, useUpdateEntry } from './api';
import type { MappingEntry } from './types';

interface EntryValues {
  sourceKeyValue: string;
  memberKey: string | null;
}

export interface EntrySheetProps {
  versionId: number;
  /** The members of the published version of the set's dimension, which the member picker offers; null while loading. */
  members: LookupItem[] | null;
  /** The entry to edit or view; null to add one. */
  entry: MappingEntry | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Adds, edits or shows an entry: a source value, stored exactly as typed, and the key of the member it maps to. */
export function EntrySheet({ versionId, members, entry, readOnly, onClose }: EntrySheetProps) {
  const create = useCreateEntry(versionId);
  const update = useUpdateEntry(versionId);
  const form = useForm<EntryValues>({
    defaultValues: { sourceKeyValue: entry?.sourceKeyValue ?? '', memberKey: entry?.memberKey ?? null },
  });

  async function submit(values: EntryValues) {
    const body = { sourceKeyValue: values.sourceKeyValue, memberKey: values.memberKey ?? '' };
    if (entry) {
      await update.mutateAsync({ entryId: entry.dimensionMappingId, body });
      return;
    }
    await create.mutateAsync(body);
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={sheetTitle(entry, readOnly)}
      description="An entry maps one actual source value to a member; within a version, each source value maps to one member."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField
        name="sourceKeyValue"
        label="Source key"
        description="The value exactly as the source holds it: 003000 and 3000 are different keys."
      />
      <KeyField
        name="memberKey"
        label="Member"
        items={members}
        description="An assignable member; requests resolve its key in the dimension's published version."
      />
    </EntityFormSheet>
  );
}

function sheetTitle(entry: MappingEntry | null, readOnly: boolean) {
  if (!entry) {
    return 'New entry';
  }
  if (readOnly) {
    return `Entry ${entry.sourceKeyValue}`;
  }
  return `Edit ${entry.sourceKeyValue}`;
}
