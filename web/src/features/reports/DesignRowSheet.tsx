import { useForm, useWatch } from 'react-hook-form';
import { Input } from '@/components/ui/input';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { FormFieldLayout, KeyField, SwitchField, TextField, VocabularyField } from '@/shared/form';
import { usePublishedVersionItems } from '@/shared/useLatestVersionItems';
import { useLookup } from '@/shared/useLookup';
import { isMemberRow, keyFromLabel, nextSortOrder, parentsFor } from './rowTree';
import type { DesignRowInput, DesignRowKind, ReportDesign, ReportVersion } from './types';

/** Values of the row sheet; a choice not made yet is null. */
interface DesignRowValues {
  label: string;
  kind: string | null;
  parentKey: string | null;
  isVisible: boolean;
  showsValues: boolean;
  calculatedMeasureKey: string | null;
  valueColumnKey: string | null;
}

export interface DesignRowSheetProps {
  version: ReportVersion;
  design: ReportDesign;
  /** Every row of the design, as the tree shows them. */
  rows: DesignRowInput[];
  /** The row to edit or view; null to add one at `placement`. */
  row: DesignRowInput | null;
  /** The kind a new row starts as and the row it starts under, or null for the top. */
  placement: { kind: DesignRowKind; parentKey: string | null } | null;
  readOnly: boolean;
  /** Saves the row into the design's rows. */
  onSave: (row: DesignRowInput) => Promise<unknown>;
  onClose: () => void;
}

/**
 * Adds, edits or shows a row of the design: a data row taking source items, which the rows under it add to, a group totalling
 * the rows under it, a section heading them, or a formula row placing a calculated measure of the model's published version in
 * one column. A new row's key is made from its label and is fixed once saved. A stored member row stays a member row and an
 * overlay row an overlay row, and a row with items stays a data row.
 */
export function DesignRowSheet({ version, design, rows, row, placement, readOnly, onSave, onClose }: DesignRowSheetProps) {
  const measures = usePublishedVersionItems('model-versions', version.modelId, 'calculated-measures');
  const columns = useLookup('report-columns', version.reportVersionId);
  const form = useForm<DesignRowValues>({
    defaultValues: {
      label: row?.label ?? '',
      kind: row?.kind ?? placement?.kind ?? null,
      parentKey: row ? (row.parentKey ?? null) : (placement?.parentKey ?? null),
      isVisible: row?.isVisible ?? true,
      showsValues: row?.showsValues ?? true,
      calculatedMeasureKey: row?.calculatedMeasureKey ?? null,
      valueColumnKey: row?.valueColumnKey ?? null,
    },
  });
  const [label, kind] = useWatch({ control: form.control, name: ['label', 'kind'] });
  const key = row?.key ?? keyFromLabel(label, new Set(rows.map(stored => stored.key)));

  function submit(values: DesignRowValues) {
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
    });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={row === null ? 'New row' : readOnly ? `Row ${row.key}` : `Edit ${row.key}`}
      description="Data and group rows are rows of the report's own dimension; sections and formula rows are laid over them."
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
    >
      <TextField name="label" label="Label" />
      <FormFieldLayout
        id="design-row-key"
        label="Key"
        description={row === null ? 'Made from the label; fixed once the row is saved.' : 'Requests and mappings name the row by it.'}
      >
        <Input id="design-row-key" className="font-mono" value={key} readOnly />
      </FormFieldLayout>
      <VocabularyField name="kind" label="Kind" vocabulary="design_row_kind" exclude={excludedKinds(design, row)} />
      <KeyField
        name="parentKey"
        label="Under"
        items={parentsFor(rows, kind ?? '', row?.key ?? null)}
        allowEmpty={!isMemberRow(kind ?? '')}
        description="Data and group rows sit under a data or group row; sections and formula rows under any row but a formula row, or at the top."
      />
      <SwitchField name="isVisible" label="Visible" description="Whether the report shows the row; the rows under a hidden row move up." />
      {kind === 'group' && (
        <SwitchField
          name="showsValues"
          label="Shows its total"
          description="Off makes the group a header above the rows under it."
        />
      )}
      {kind === 'formula' && (
        <>
          <KeyField name="calculatedMeasureKey" label="Calculated measure" items={measures} />
          <KeyField
            name="valueColumnKey"
            label="Column"
            items={columns.data ?? null}
            description="The saved column the value is placed in; save new columns first."
          />
        </>
      )}
    </EntityFormSheet>
  );
}

/**
 * The kinds a row can't take: a stored member row stays a data or group row and an overlay row a section or formula row, and
 * a row that source items map to stays a data row.
 */
function excludedKinds(design: ReportDesign, row: DesignRowInput | null): DesignRowKind[] {
  if (row === null) {
    return [];
  }
  if (!isMemberRow(row.kind)) {
    return ['data', 'group'];
  }
  const hasItems = design.rows.some(stored => stored.key === row.key && stored.mappings.length > 0);
  return hasItems ? ['section', 'formula', 'group'] : ['section', 'formula'];
}
