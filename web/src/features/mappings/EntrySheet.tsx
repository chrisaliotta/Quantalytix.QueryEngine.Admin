import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, TextField } from '@/shared/form';
import { useCreateEntry, useUpdateEntry } from './api';
import type { MappingEntry } from './types';

interface EntryValues {
  sourceKeyValue: string;
  memberId: number | null;
}

export interface EntrySheetProps {
  versionId: number;
  /** The dimension version the mapping version pins; the member picker offers its members. */
  dimensionVersionId: number | null;
  /** The entry to edit or view; null to add one. */
  entry: MappingEntry | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Adds, edits or shows an entry: a source value, stored exactly as typed, and the member it maps to. */
export function EntrySheet({ versionId, dimensionVersionId, entry, readOnly, onClose }: EntrySheetProps) {
  const create = useCreateEntry(versionId);
  const update = useUpdateEntry(versionId);
  const form = useForm<EntryValues>({
    defaultValues: { sourceKeyValue: entry?.sourceKeyValue ?? '', memberId: entry?.memberId ?? null },
  });

  async function submit(values: EntryValues) {
    const body = { sourceKeyValue: values.sourceKeyValue, memberId: values.memberId ?? 0 };
    if (entry) {
      await update.mutateAsync({ entryId: entry.mappingEntryId, body });
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
      <EntityField
        name="memberId"
        label="Member"
        lookupKind="members"
        parentId={dimensionVersionId}
        description="An assignable member of the pinned dimension version."
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
