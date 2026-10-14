import { DateTimeField, TextAreaField, TextField } from '@/shared/form';

/** The settings of an assumption set that stay editable while it is a draft: name, description and effective time. */
export function AssumptionSetSettingsFields() {
  return (
    <>
      <TextField name="displayName" label="Display name" />
      <TextAreaField name="description" label="Description" description="The business purpose or provenance of the snapshot." />
      <DateTimeField
        name="effectiveAt"
        label="Effective at"
        description="Capturing pins, for each dimension, the published version effective at this time."
      />
    </>
  );
}
