import { JsonField, SwitchField, TextField } from '@/shared/form';

/** The fields of a report's identity attributes (`ReportValues`). */
export function ReportIdentityFields() {
  return (
    <>
      <TextField name="displayName" label="Name" />
      <TextField name="reportType" label="Type" description="A classification clients group reports by, such as margin_analysis." />
      <SwitchField name="isEnabled" label="Enabled" description="Whether the engine offers the report." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </>
  );
}
