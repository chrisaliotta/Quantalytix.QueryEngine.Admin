import { useForm, useWatch } from 'react-hook-form';
import { Input } from '@/components/ui/input';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { FormFieldLayout, KeyField, NumberField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { keyItems } from '@/shared/keyItems';
import { useLookup } from '@/shared/useLookup';
import { useReportStyles } from './api';
import { inheritedValue, isMemberRow, keyFromLabel, nextSortOrder, parentsFor, rowProblem } from './rowTree';
import type { EditorView, ModelParameter, ReportRowInput, ReportRowKind, RowParameterValue } from './types';

/** Values of the row sheet; a choice not made yet is null, and so is a parameter the row sets no value of. */
interface RowValues {
  label: string;
  kind: string | null;
  parentKey: string | null;
  isVisible: boolean;
  showsValues: boolean;
  calculatedMeasureKey: string | null;
  valueColumnKey: string | null;
  styleKey: string | null;
  parameters: Record<string, RowParameterValue | null>;
}

export interface RowSheetProps {
  view: EditorView;
  /** Every row of the report, as the tree shows them. */
  rows: ReportRowInput[];
  /** The row to edit; null to add one at `placement`. */
  row: ReportRowInput | null;
  /** The kind a new row starts as and the row it starts under, or null for the top. */
  placement: { kind: ReportRowKind; parentKey: string | null } | null;
  /** Saves the row into the report's rows. */
  onSave: (row: ReportRowInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds or edits a row of the report: a data row taking items from the bridge sources and a direct source's rows keyed like it,
 * which the rows under it add to, a group totalling the rows under it, a section heading them, or a formula row placing a
 * calculated measure of the report's model in one column. A new row's key is made from its label and is fixed once saved. A
 * stored member row stays a member row and an overlay row an overlay row, and a row with items stays a data row. A row names its
 * style and sets its own values of the model's parameters; a parameter it leaves empty shows the value it inherits.
 */
export function RowSheet({ view, rows, row, placement, onSave, onClose }: RowSheetProps) {
  const measures = useLookup('calculated-measures', view.report.calculationModelId);
  const columns = useLookup('report-columns', view.report.reportId);
  const styles = useReportStyles(view.report.reportId);
  const form = useForm<RowValues>({
    defaultValues: {
      label: row?.label ?? '',
      kind: row?.kind ?? placement?.kind ?? null,
      parentKey: row ? (row.parentKey ?? null) : (placement?.parentKey ?? null),
      isVisible: row?.isVisible ?? true,
      showsValues: row?.showsValues ?? true,
      calculatedMeasureKey: row?.calculatedMeasureKey ?? null,
      valueColumnKey: row?.valueColumnKey ?? null,
      styleKey: row?.styleKey ?? null,
      parameters: Object.fromEntries(view.parameters.map(parameter => [parameter.parameterKey, row?.parameters?.[parameter.parameterKey] ?? null])),
    },
  });
  const [label, kind, parentKey] = useWatch({ control: form.control, name: ['label', 'kind', 'parentKey'] });
  const key = row?.key ?? keyFromLabel(label, new Set(rows.map(stored => stored.key)));
  const index = row === null ? rows.length : rows.findIndex(stored => stored.key === row.key);

  function submit(values: RowValues) {
    const isFormula = values.kind === 'formula';
    const keepsPlace = row !== null && (row.parentKey ?? null) === values.parentKey;
    return onSave({
      key,
      label: values.label,
      kind: values.kind ?? '',
      parentKey: values.parentKey,
      sortOrder: keepsPlace ? row.sortOrder : nextSortOrder(rows, values.parentKey),
      isVisible: values.isVisible,
      showsValues: values.kind === 'group' ? values.showsValues : true,
      calculatedMeasureKey: isFormula ? values.calculatedMeasureKey : null,
      valueColumnKey: isFormula ? values.valueColumnKey : null,
      styleKey: values.styleKey,
      parameters: Object.fromEntries(
        Object.entries(values.parameters).filter((entry): entry is [string, RowParameterValue] => entry[1] !== null && entry[1] !== '')
      ),
    }).catch(error => {
      throw rowProblem(error, index);
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={row === null ? 'New row' : `Edit ${row.key}`}
      description="Data and group rows are rows of the report's own dimension; sections and formula rows are laid over them."
      form={form}
      onSubmit={submit}
    >
      <TextField name="label" label="Label" />
      <FormFieldLayout
        id="report-row-key"
        label="Key"
        description={
          row === null
            ? 'Made from the label; fixed once the row is saved.'
            : "Requests, item mappings and a direct source's field values name the row by it."
        }
      >
        <Input id="report-row-key" className="font-mono" value={key} readOnly />
      </FormFieldLayout>
      <VocabularyField name="kind" label="Kind" vocabulary="report_row_kind" exclude={excludedKinds(view, row)} />
      <KeyField
        name="parentKey"
        label="Under"
        items={parentsFor(rows, kind ?? '', row?.key ?? null)}
        allowEmpty={!isMemberRow(kind ?? '')}
        description="Data and group rows sit under a data or group row; sections and formula rows under any row but a formula row, or at the top."
      />
      <SwitchField name="isVisible" label="Visible" description="Whether the report shows the row; the rows under a hidden row move up." />
      {kind === 'group' && (
        <SwitchField name="showsValues" label="Shows its total" description="Off makes the group a header above the rows under it." />
      )}
      {kind === 'formula' && (
        <>
          <KeyField name="calculatedMeasureKey" label="Calculated measure" items={measures.data ?? null} />
          <KeyField
            name="valueColumnKey"
            label="Column"
            items={columns.data ?? null}
            description="The saved column the value is placed in; save new columns first."
          />
        </>
      )}
      <KeyField
        name="styleKey"
        label="Style"
        items={styles.data?.map((style, position) => ({ id: position, key: style.styleKey, label: style.displayName })) ?? null}
        allowEmpty
        placeholder="Its role's default style"
        description="How the report shows the row; a row without a style shows its role's default, if the report has one."
      />
      {view.parameters.length > 0 && (
        <section data-slot="row-parameters" className="space-y-4 border-t pt-4">
          <p className="text-sm text-muted-foreground">
            The row's own values of the model's parameters. An empty one shows the value the row inherits: its nearest ancestor's,
            or the total's for a formula row or a top-level section, else the model's default.
          </p>
          {view.parameters.map(parameter => (
            <ParameterField
              key={parameter.parameterKey}
              parameter={parameter}
              inherited={inheritedValue(rows, kind ?? '', parentKey, parameter)}
            />
          ))}
        </section>
      )}
    </EntityFormSheet>
  );
}

/** A row's own value of one parameter, picked from its allowed values when it has them, with the inherited value as placeholder. */
function ParameterField({ parameter, inherited }: { parameter: ModelParameter; inherited: RowParameterValue }) {
  const name = `parameters.${parameter.parameterKey}`;
  const label = `${parameter.displayName} (@${parameter.parameterKey})`;
  const placeholder = `${inherited} (inherited)`;
  const description = parameter.description ?? undefined;
  if (parameter.valueType === 'number') {
    return <NumberField name={name} label={label} placeholder={placeholder} description={description} />;
  }
  if (parameter.allowedValues) {
    return (
      <KeyField name={name} label={label} items={keyItems(parameter.allowedValues)} allowEmpty placeholder={placeholder} description={description} />
    );
  }
  return <TextField name={name} label={label} placeholder={placeholder} description={description} />;
}

/**
 * The kinds a row can't take: a stored member row stays a data or group row and an overlay row a section or formula row, and a
 * row that items map to stays a data row.
 */
function excludedKinds(view: EditorView, row: ReportRowInput | null): ReportRowKind[] {
  if (row === null) {
    return [];
  }
  if (!isMemberRow(row.kind)) {
    return ['data', 'group'];
  }
  const hasItems = view.rows.some(stored => stored.key === row.key && stored.items.length > 0);
  return hasItems ? ['section', 'formula', 'group'] : ['section', 'formula'];
}
