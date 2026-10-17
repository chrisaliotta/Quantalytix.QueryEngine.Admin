import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, JsonField } from '@/shared/form';
import { useUpdateReportVersion } from './api';
import { optionalJson } from './reportForm';
import type { ReportVersion } from './types';

/** Values of the version settings sheet; the defaults are JSON text. */
interface ReportVersionValues {
  defaultSourceProfileId: number | null;
  payloadJson: string | null;
  defaultFilterJson: string | null;
  defaultSettingsJson: string | null;
  defaultPivotJson: string | null;
}

export interface ReportVersionSheetProps {
  /** The draft version to edit. */
  version: ReportVersion;
  onClose: () => void;
}

/**
 * Edits a draft version's default profile and defaults. Only `periodicity` and `timeZone` of the settings change how
 * the engine runs the report; the rest, and the payload, pass through to clients.
 */
export function ReportVersionSheet({ version, onClose }: ReportVersionSheetProps) {
  const update = useUpdateReportVersion(version.reportVersionId);
  const form = useForm<ReportVersionValues>({
    defaultValues: {
      defaultSourceProfileId: version.defaultSourceProfileId ?? null,
      payloadJson: version.payloadJson ?? null,
      defaultFilterJson: version.defaultFilterJson ?? null,
      defaultSettingsJson: version.defaultSettingsJson ?? null,
      defaultPivotJson: version.defaultPivotJson ?? null,
    },
  });

  function submit(values: ReportVersionValues) {
    return update.mutateAsync({
      defaultSourceProfileId: values.defaultSourceProfileId,
      payloadJson: optionalJson(values.payloadJson),
      defaultFilterJson: optionalJson(values.defaultFilterJson),
      defaultSettingsJson: optionalJson(values.defaultSettingsJson),
      defaultPivotJson: optionalJson(values.defaultPivotJson),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Edit v${version.versionNumber}`}
      description="Validating the version checks that the filter and pivot name calculation dimensions that allow them."
      form={form}
      onSubmit={submit}
      width="40rem"
    >
      <EntityField
        name="defaultSourceProfileId"
        label="Default profile"
        lookupKind="source-profiles"
        parentId={version.modelVersionId ?? null}
        allowEmpty
      />
      <JsonField name="defaultSettingsJson" label="Default settings" description='Such as {"periodicity":"month","timeZone":"UTC"}.' />
      <JsonField
        name="defaultPivotJson"
        label="Default pivot"
        description='The calculation dimensions to break out by, such as {"rowDimensions":["branch"]}.'
      />
      <JsonField
        name="defaultFilterJson"
        label="Default filter"
        description="A structured filter over calculation dimensions, ANDed with every request's filter."
      />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
