import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { TextField, VocabularyField } from '@/shared/form';
import { emptyToNull } from '@/shared/text';
import { useSaveModelContent } from './api';
import { contentSheetTitle } from './contentSheet';
import type { ModelContentInput, Variable } from './types';

/** Values of the variable sheet; a choice not made yet is null, and an optional text is empty for none. */
interface VariableValues {
  variableKey: string;
  displayName: string;
  variableRole: string;
  targetMemberKey: string;
  selectionScope: string | null;
  missingBehavior: string | null;
}

export interface VariableSheetProps {
  versionId: number;
  /** The variable to edit or view; null to create one. */
  variable: Variable | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Creates, edits or shows a variable: its key, role, the report item it reads and how missing values count. */
export function VariableSheet({ versionId, variable, readOnly, onClose }: VariableSheetProps) {
  const save = useSaveModelContent('variables', versionId, variable?.variableId ?? null);
  const form = useForm<VariableValues>({
    defaultValues: {
      variableKey: variable?.variableKey ?? '',
      displayName: variable?.displayName ?? '',
      variableRole: variable?.variableRole ?? '',
      targetMemberKey: variable?.targetMemberKey ?? '',
      selectionScope: variable?.selectionScope ?? 'descendants',
      missingBehavior: variable?.missingBehavior ?? 'null',
    },
  });

  function submit(values: VariableValues) {
    const body: ModelContentInput<'variables'> = {
      ...values,
      targetMemberKey: emptyToNull(values.targetMemberKey),
      selectionScope: values.selectionScope ?? '',
      missingBehavior: values.missingBehavior ?? '',
    };
    return save(body);
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
      <TextField name="displayName" label="Name" />
      <TextField name="variableRole" label="Role" description="What the variable means to the model, such as average_balance." />
      <TextField
        name="targetMemberKey"
        label="Target member"
        description="The report item the variable reads at report scope, such as LOANS; empty reads every mapped item."
      />
      <VocabularyField name="selectionScope" label="Scope" vocabulary="selection_scope" />
      <VocabularyField
        name="missingBehavior"
        label="Missing values"
        vocabulary="variable_missing_behavior"
        description="Whether a line or period without rows reads as null or as zero."
      />
    </EntityFormSheet>
  );
}
