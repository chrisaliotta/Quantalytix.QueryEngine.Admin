import { TextAreaField, TextField } from '@/shared/form';

/** The labels of an assumption set: its display name and description. */
export function AssumptionSetSettingsFields() {
  return (
    <>
      <TextField name="displayName" label="Display name" />
      <TextAreaField name="description" label="Description" description="The business purpose or provenance of the snapshot." />
    </>
  );
}
