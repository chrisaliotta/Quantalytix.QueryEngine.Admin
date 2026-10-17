import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useSaveModelContent } from './api';
import { contentSheetTitle } from './contentSheet';
import { ProfilePicksField } from './ProfilePicksField';
import type { ModelContentInput, SourceProfile } from './types';

export interface SourceProfileSheetProps {
  versionId: number;
  /** The profile to edit or view; null to create one. */
  profile: SourceProfile | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Creates, edits or shows a source profile and its picks, one source per variable. */
export function SourceProfileSheet({ versionId, profile, readOnly, onClose }: SourceProfileSheetProps) {
  const save = useSaveModelContent('profiles', versionId, profile?.profile.sourceProfileId ?? null);
  const form = useForm<ModelContentInput<'profiles'>>({
    defaultValues: {
      profileKey: profile?.profile.profileKey ?? '',
      displayName: profile?.profile.displayName ?? '',
      isEnabled: profile?.profile.isEnabled ?? true,
      picks: profile?.picks.map(pick => ({ variableId: pick.variableId, variableSourceId: pick.variableSourceId })) ?? [],
    },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('profile', profile?.profile.profileKey ?? null, readOnly)}
      description="Publishing the model version needs every enabled profile to pick an enabled source for every variable."
      form={form}
      onSubmit={save}
      readOnly={readOnly}
      width="36rem"
    >
      <TextField name="profileKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <SwitchField name="isEnabled" label="Enabled" description="Whether reports may run under the profile." />
      <ProfilePicksField name="picks" label="Picks" versionId={versionId} />
    </EntityFormSheet>
  );
}
