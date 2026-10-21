import { JsonField, TextField } from '@/shared/form';

/** The fields of a report's identity attributes (`ReportValues`). */
export function ReportIdentityFields() {
  return (
    <>
      <TextField name="displayName" label="Name" />
      <TextField name="reportType" label="Type" description="A classification clients group reports by, such as margin_analysis." />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </>
  );
}
