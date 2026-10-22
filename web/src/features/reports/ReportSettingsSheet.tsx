import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { JsonField, KeyField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useLookup } from '@/shared/useLookup';
import { useUpdateReportSettings } from './api';
import type { Report } from './types';

/** Values of the settings sheet; a choice not made yet is null, and the defaults are JSON text, empty for none. */
interface ReportSettingsValues {
  defaultProfileKey: string | null;
  subtotalPosition: string | null;
  defaultFilterJson: string;
  defaultSettingsJson: string;
  defaultPivotJson: string;
}

export interface ReportSettingsSheetProps {
  report: Report;
  onClose: () => void;
}

/**
 * Edits a report's default profile, subtotal position and defaults. The profile is named by key, offered from the report's own
 * profiles. Only `periodicity`, `timeZone` and `layout` of the settings change how the engine runs the report; the rest pass
 * through to clients. An enabled report refuses settings that break its rules, naming them.
 */
export function ReportSettingsSheet({ report, onClose }: ReportSettingsSheetProps) {
  const update = useUpdateReportSettings(report.reportId);
  const profiles = useLookup('source-profiles', report.reportId);
  const form = useForm<ReportSettingsValues>({
    defaultValues: {
      defaultProfileKey: report.defaultProfileKey ?? null,
      subtotalPosition: report.subtotalPosition,
      defaultFilterJson: report.defaultFilterJson ?? '',
      defaultSettingsJson: report.defaultSettingsJson ?? '',
      defaultPivotJson: report.defaultPivotJson ?? '',
    },
  });

  function submit(values: ReportSettingsValues) {
    return update.mutateAsync({
      defaultProfileKey: values.defaultProfileKey,
      subtotalPosition: values.subtotalPosition ?? '',
      defaultFilterJson: emptyToNull(values.defaultFilterJson),
      defaultSettingsJson: emptyToNull(values.defaultSettingsJson),
      defaultPivotJson: emptyToNull(values.defaultPivotJson),
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Settings of ${report.reportKey}`}
      description="Enabling the report checks that the default profile is enabled and that the filter and pivot name calculation dimensions that allow them."
      form={form}
      onSubmit={submit}
      width="40rem"
    >
      <KeyField
        name="defaultProfileKey"
        label="Default profile"
        items={profiles.data ?? null}
        allowEmpty
        description="The profile a request runs under when it names none; an enabled report needs an enabled one."
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
    </EntityFormSheet>
  );
}
