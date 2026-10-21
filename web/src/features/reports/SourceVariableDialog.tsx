import { useForm } from 'react-hook-form';
import { JsonField, KeyField } from '@/shared/form';
import { FormDialog } from '@/shared/FormDialog';
import { emptyToNull } from '@/shared/text';
import { useLatestVersionItems } from '@/shared/useLatestVersionItems';
import { useReplaceSourceVariables } from './editorApi';
import type { EditorSource, EditorView, SourceVariableInput } from './types';

/** Values of the binding dialog; no measure means the source does not supply the variable, and the filter is JSON text. */
interface SourceVariableValues {
  measureKey: string | null;
  sourceFilterJson: string;
}

export interface SourceVariableDialogProps {
  view: EditorView;
  source: EditorSource;
  /** The model variable the source supplies or stops supplying. */
  variableKey: string;
  onClose: () => void;
}

/**
 * Binds one model variable to a measure of a source's dataset, with an optional filter over the dataset's fields, or unbinds it.
 * The source's other variables stay as they are; a profile that picks no source for the variable picks this one.
 */
export function SourceVariableDialog({ view, source, variableKey, onClose }: SourceVariableDialogProps) {
  const replace = useReplaceSourceVariables(view.report.reportId);
  const measures = useLatestVersionItems('dataset-versions', source.source.datasetId, 'measures');
  const current = source.variables.find(variable => variable.variableKey === variableKey);
  const form = useForm<SourceVariableValues>({
    defaultValues: { measureKey: current?.measureKey ?? null, sourceFilterJson: current?.sourceFilterJson ?? '' },
  });

  function submit(values: SourceVariableValues) {
    const others: SourceVariableInput[] = source.variables
      .filter(variable => variable.variableKey !== variableKey)
      .map(({ variableKey: key, measureKey, sourceFilterJson }) => ({ variableKey: key, measureKey, sourceFilterJson }));
    const variables = values.measureKey
      ? [...others, { variableKey, measureKey: values.measureKey, sourceFilterJson: emptyToNull(values.sourceFilterJson) }]
      : others;
    return replace.mutateAsync({ sourceId: source.source.reportSourceId, variables });
  }

  return (
    <FormDialog
      open
      onOpenChange={open => !open && onClose()}
      title={`${variableKey} from ${source.source.displayName}`}
      description={`Which measure of ${source.datasetKey} supplies the variable; no measure means the source does not supply it.`}
      form={form}
      onSubmit={submit}
      submitLabel="Save"
    >
      <KeyField
        name="measureKey"
        label="Measure"
        items={measures}
        allowEmpty
        description="An enabled measure of the dataset version the active release pins."
      />
      <JsonField
        name="sourceFilterJson"
        label="Filter"
        description={`Optional: a structured filter over the dataset's filterable fields, such as {"operator":"eq","left":{"type":"field","id":"accountKey"},"right":{"type":"literal","value":"4000"}}.`}
      />
    </FormDialog>
  );
}
