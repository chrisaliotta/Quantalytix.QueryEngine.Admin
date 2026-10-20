import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { SwitchField, TextField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import type { ReportRowNode } from './rowTree';
import type { ReportMemberOptionInput } from './types';

/** Values of the member option sheet; an empty name shows the member's own. */
interface ReportMemberOptionValues {
  displayName: string;
  isVisible: boolean;
  showsValues: boolean;
}

export interface ReportMemberOptionSheetProps {
  /** The member row whose option is edited or shown. */
  row: ReportRowNode;
  readOnly: boolean;
  /** Saves the option into the version's member options. */
  onSave: (option: ReportMemberOptionInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Edits or shows how the report version shows one member of the report dimension: its name, whether it is shown, and
 * whether it shows values or only heads the rows under it. A member without an option shows its own name and values.
 */
export function ReportMemberOptionSheet({ row, readOnly, onSave, onClose }: ReportMemberOptionSheetProps) {
  const form = useForm<ReportMemberOptionValues>({
    defaultValues: {
      displayName: row.option?.displayName ?? '',
      isVisible: row.option?.isVisible ?? true,
      showsValues: row.option?.showsValues ?? true,
    },
  });

  function submit(values: ReportMemberOptionValues) {
    return onSave({ memberKey: row.key, ...values, displayName: emptyToNull(values.displayName) });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={readOnly ? `Member ${row.key}` : `Edit ${row.key}`}
      description="The member's place and children come from the report dimension's published version."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField name="displayName" label="Name" description={`Optional; empty shows the member's own name, ${row.name}.`} />
      <SwitchField name="isVisible" label="Visible" description="Whether the report table shows the row; the rows under a hidden row move up." />
      <SwitchField
        name="showsValues"
        label="Shows values"
        description="Whether the row shows the member's totals; a row without values heads the rows under it, before them."
      />
    </EntityFormSheet>
  );
}
