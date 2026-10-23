import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useSaveOwnedContent, type ContentInput } from '@/shared/ownedContent';
import { ProfilePicksField } from './ProfilePicksField';
import type { EditorView, SourceProfile } from './types';

export interface SourceProfileSheetProps {
  view: EditorView;
  /** The profile to edit; null to create one. */
  profile: SourceProfile | null;
  onClose: () => void;
}

/** Creates or edits a source profile and its picks, one source per variable. */
export function SourceProfileSheet({ view, profile, onClose }: SourceProfileSheetProps) {
  const save = useSaveOwnedContent('profiles', view.report.reportId, profile?.profile.calculationSourceProfileId ?? null);
  const form = useForm<ContentInput<'profiles'>>({
    defaultValues: {
      profileKey: profile?.profile.profileKey ?? '',
      displayName: profile?.profile.displayName ?? '',
      isEnabled: profile?.profile.isEnabled ?? true,
      picks: profile?.picks.map(pick => ({ variableKey: pick.variableKey, reportSourceId: pick.reportSourceId })) ?? [],
    },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('profile', profile?.profile.profileKey ?? null)}
      description="An enabled report needs every enabled profile to pick a source for every variable its columns and formula rows read."
      form={form}
      onSubmit={save}
      width="36rem"
    >
      <TextField name="profileKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <SwitchField name="isEnabled" label="Enabled" description="Whether requests may run under the profile." />
      <ProfilePicksField name="picks" label="Picks" view={view} />
    </EntityFormSheet>
  );
}
