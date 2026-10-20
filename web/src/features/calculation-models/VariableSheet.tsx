import { useForm } from 'react-hook-form';
import { contentSheetTitle } from '@/shared/contentSheet';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useSaveVersionContent } from '@/shared/versionContent';
import type { Variable } from './types';

/** Values of the variable sheet; a choice not made yet is null, and an optional text is empty for none. */
interface VariableValues {
  variableKey: string;
  displayName: string;
  variableRole: string;
  missingBehavior: string | null;
  displayFormat: string;
  unitKey: string;
}

export interface VariableSheetProps {
  versionId: number;
  /** The variable to edit or view; null to create one. */
  variable: Variable | null;
  readOnly: boolean;
  onClose: () => void;
}

/**
 * Creates, edits or shows a variable: its key, role, how missing values count and how clients show it. Which rows it reads
 * is each report's choice, through the report's sources and targets.
 */
export function VariableSheet({ versionId, variable, readOnly, onClose }: VariableSheetProps) {
  const save = useSaveVersionContent('variables', versionId, variable?.variableId ?? null);
  const form = useForm<VariableValues>({
    defaultValues: {
      variableKey: variable?.variableKey ?? '',
      displayName: variable?.displayName ?? '',
      variableRole: variable?.variableRole ?? '',
      missingBehavior: variable?.missingBehavior ?? 'null',
      displayFormat: variable?.displayFormat ?? 'number',
      unitKey: variable?.unitKey ?? '',
    },
  });

  function submit(values: VariableValues) {
    return save({ ...values, missingBehavior: values.missingBehavior ?? '', unitKey: emptyToNull(values.unitKey) });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={contentSheetTitle('variable', variable?.variableKey ?? null, readOnly)}
      description="Variables and calculated measures share one key namespace, because a formula names either as [m.KEY]."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField name="variableKey" label="Key" description="A letter, then letters, digits or underscores." />
      <TextField name="displayName" label="Name" description="The label a report column shows unless it names its own." />
      <TextField name="variableRole" label="Role" description="What the variable means to the model, such as average_balance." />
      <VocabularyField
        name="missingBehavior"
        label="Missing values"
        vocabulary="variable_missing_behavior"
        description="Whether a row or period without data reads as null or as zero."
      />
      <TextField
        name="displayFormat"
        label="Display format"
        description="How clients format the value unless a report column names its own, such as currency."
      />
      <TextField name="unitKey" label="Unit" description="Optional, such as USD." />
    </EntityFormSheet>
  );
}
