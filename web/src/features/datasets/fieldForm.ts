import { emptyToNull } from '@/shared/text';
import type { Field, FieldInput, FieldUpdateInput, SourceColumn } from './types';

/** Values of the field sheet; text inputs hold '' where the API takes null. */
export interface FieldFormValues {
  fieldKey: string;
  displayName: string;
  fieldSourceKind: string | null;
  sourceColumn: string | null;
  jsonPath: string;
  expressionText: string;
  dataType: string | null;
  numericPrecision: number | null;
  numericScale: number | null;
  isNullable: boolean;
  unitKey: string;
  isSelectable: boolean;
  isFilterable: boolean;
  isSortable: boolean;
  isGroupable: boolean;
  isEnabled: boolean;
}

/** Sheet values of a field, or of a new column field with the schema's default flags. */
export function fieldFormValues(field?: Field): FieldFormValues {
  return {
    fieldKey: field?.fieldKey ?? '',
    displayName: field?.displayName ?? '',
    fieldSourceKind: field?.fieldSourceKind ?? 'column',
    sourceColumn: field?.sourceColumn ?? null,
    jsonPath: field?.jsonPath ?? '',
    expressionText: field?.expressionText ?? '',
    dataType: field?.dataType ?? null,
    numericPrecision: field?.numericPrecision ?? null,
    numericScale: field?.numericScale ?? null,
    isNullable: field?.isNullable ?? true,
    unitKey: field?.unitKey ?? '',
    isSelectable: field?.isSelectable ?? true,
    isFilterable: field?.isFilterable ?? true,
    isSortable: field?.isSortable ?? true,
    isGroupable: field?.isGroupable ?? false,
    isEnabled: field?.isEnabled ?? true,
  };
}

/**
 * The `PUT` body of the sheet's values. The sheet shows only the source inputs of the chosen kind, and precision and
 * scale only for decimals, so values left behind in hidden inputs are not sent; empty text is sent as null.
 */
export function toFieldUpdateInput(values: FieldFormValues): FieldUpdateInput {
  const kind = values.fieldSourceKind;
  const isDecimal = values.dataType === 'decimal';
  return {
    displayName: values.displayName,
    fieldSourceKind: kind ?? '',
    sourceColumn: readsColumn(kind) ? values.sourceColumn : null,
    jsonPath: kind === 'json_path' ? emptyToNull(values.jsonPath) : null,
    expressionText: kind === 'expression' ? emptyToNull(values.expressionText) : null,
    dataType: values.dataType ?? '',
    numericPrecision: isDecimal ? values.numericPrecision : null,
    numericScale: isDecimal ? values.numericScale : null,
    isNullable: values.isNullable,
    unitKey: emptyToNull(values.unitKey),
    isSelectable: values.isSelectable,
    isFilterable: values.isFilterable,
    isSortable: values.isSortable,
    isGroupable: values.isGroupable,
    isEnabled: values.isEnabled,
  };
}

/** The `POST` body of the sheet's values: the update body plus the key. */
export function toFieldInput(values: FieldFormValues): FieldInput {
  return { fieldKey: values.fieldKey, ...toFieldUpdateInput(values) };
}

/**
 * The columns "Add from columns" offers: those with a field type that no column field of the version reads yet.
 * StarRocks column names ignore case.
 */
export function unusedColumns(columns: SourceColumn[], fields: Field[]): SourceColumn[] {
  const read = new Set(
    fields
      .filter(field => field.fieldSourceKind === 'column' && field.sourceColumn)
      .map(field => field.sourceColumn?.toLowerCase())
  );
  return columns.filter(column => column.dataType && !read.has(column.name.toLowerCase()));
}

/** Whether a source kind reads a physical column: column fields read it, json_path fields read JSON from it. */
export function readsColumn(kind: string | null) {
  return kind === 'column' || kind === 'json_path';
}
