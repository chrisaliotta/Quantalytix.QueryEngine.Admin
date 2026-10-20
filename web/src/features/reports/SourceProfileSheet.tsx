import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { useSaveVersionContent, type ContentInput } from '@/shared/versionContent';
import { ProfilePicksField } from './ProfilePicksField';
import type { ReportVersion, SourceProfile } from './types';

export interface SourceProfileSheetProps {
  version: ReportVersion;
  /** The profile to edit or view; null to create one. */
  profile: SourceProfile | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Creates, edits or shows a source profile and its picks, one source per variable. */
export function SourceProfileSheet({ version, profile, readOnly, onClose }: SourceProfileSheetProps) {
  const save = useSaveVersionContent('profiles', version.reportVersionId, profile?.profile.sourceProfileId ?? null);
  const form = useForm<ContentInput<'profiles'>>({
    defaultValues: {
      profileKey: profile?.profile.profileKey ?? '',
      displayName: profile?.profile.displayName ?? '',
      isEnabled: profile?.profile.isEnabled ?? true,
      picks: profile?.picks.map(pick => ({ variableKey: pick.variableKey, variableSourceId: pick.variableSourceId })) ?? [],
    },
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('profile', profile?.profile.profileKey ?? null, readOnly)}
      description="Publishing the report version needs every enabled profile to pick an enabled source for every variable its columns and formula rows read."
      form={form}
      onSubmit={save}
      readOnly={readOnly}
      width="36rem"
    >
      <TextField name="profileKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" />
      <SwitchField name="isEnabled" label="Enabled" description="Whether requests may run under the profile." />
      <ProfilePicksField name="picks" label="Picks" version={version} />
    </EntityFormSheet>
  );
}
