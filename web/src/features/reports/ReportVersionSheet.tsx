import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLookup } from '@/shared/useLookup';
import { useUpdateReportVersion } from './api';
import type { ReportVersion } from './types';

/** Values of the version settings sheet; a choice not made yet is null, and the defaults are JSON text, empty for none. */
interface ReportVersionValues {
  defaultProfileKey: string | null;
  subtotalPosition: string | null;
  payloadJson: string;
  defaultFilterJson: string;
  defaultSettingsJson: string;
  defaultPivotJson: string;
}

export interface ReportVersionSheetProps {
  /** The draft or published version to edit. */
  version: ReportVersion;
  onClose: () => void;
}

/**
 * Edits a version's default profile, subtotal position and defaults. The profile is named by key, offered from the version's
 * own profiles. Only `periodicity` and `timeZone` of the settings change how the engine runs the report; the rest, and the
 * payload, pass through to clients.
 */
export function ReportVersionSheet({ version, onClose }: ReportVersionSheetProps) {
  const update = useUpdateReportVersion(version.reportVersionId);
  const profiles = useLookup('source-profiles', version.reportVersionId);
  const form = useForm<ReportVersionValues>({
    defaultValues: {
      defaultProfileKey: version.defaultProfileKey ?? null,
      subtotalPosition: version.subtotalPosition,
      payloadJson: version.payloadJson ?? '',
      defaultFilterJson: version.defaultFilterJson ?? '',
      defaultSettingsJson: version.defaultSettingsJson ?? '',
      defaultPivotJson: version.defaultPivotJson ?? '',
    },
  });

  function submit(values: ReportVersionValues) {
    return update.mutateAsync({
      defaultProfileKey: values.defaultProfileKey,
      subtotalPosition: values.subtotalPosition ?? '',
      payloadJson: emptyToNull(values.payloadJson),
      defaultFilterJson: emptyToNull(values.defaultFilterJson),
      defaultSettingsJson: emptyToNull(values.defaultSettingsJson),
      defaultPivotJson: emptyToNull(values.defaultPivotJson),
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
      <KeyField
        name="defaultProfileKey"
        label="Default profile"
        items={profiles.data ?? null}
        allowEmpty
        description="The profile a request runs under when it names none; publishing needs an enabled one."
      />
      <VocabularyField
        name="subtotalPosition"
        label="Subtotals"
        vocabulary="subtotal_position"
        description="Whether a member's values come before or after the rows under it; a member without values always comes first."
      />
      <JsonField
        name="defaultSettingsJson"
        label="Default settings"
        description='Such as {"periodicity":"month","timeZone":"UTC","layout":"statement"}. A list layout lets requests filter, order and page the whole table.'
      />
      <JsonField
        name="defaultPivotJson"
        label="Default pivot"
        description='The calculation dimensions to break out by, such as {"rowDimensions":["branch"]}.'
      />
      <JsonField
        name="defaultFilterJson"
        label="Default filter"
        description="A structured filter over calculation dimensions, ANDed with every request's where. A parameter operand makes every request supply its value."
      />
      <JsonField name="payloadJson" label="Payload" description="Optional JSON that clients read; the engine passes it through." />
    </EntityFormSheet>
  );
}
