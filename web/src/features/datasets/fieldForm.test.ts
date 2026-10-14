/**
 * Why these tests exist: the field sheet shows only the inputs of the chosen source kind, and precision and scale only
 * for decimals. Values the user typed before switching kind or type stay in the hidden inputs; sending them would fail
 * API validation on inputs the user can no longer see. They also pin which columns "Add from columns" offers.
 */
import { describe, expect, it } from 'vitest';
import { fieldFormValues, toFieldInput, toFieldUpdateInput, unusedColumns } from './fieldForm';
import type { Field, SourceColumn } from './types';

describe('toFieldInput', () => {
  it('sends only the source inputs of the chosen kind', () => {
    const values = {
      ...fieldFormValues(),
      fieldKey: 'region',
      displayName: 'Region',
      sourceColumn: 'attributes',
      jsonPath: '$.region',
      expressionText: '[f.state]',
      dataType: 'string',
    };

    expect(toFieldInput({ ...values, fieldSourceKind: 'column' })).toMatchObject({
      sourceColumn: 'attributes',
      jsonPath: null,
      expressionText: null,
    });
    expect(toFieldInput({ ...values, fieldSourceKind: 'json_path' })).toMatchObject({
      sourceColumn: 'attributes',
      jsonPath: '$.region',
      expressionText: null,
    });
    expect(toFieldInput({ ...values, fieldSourceKind: 'expression' })).toMatchObject({
      sourceColumn: null,
      jsonPath: null,
      expressionText: '[f.state]',
    });
  });

  it('sends precision and scale only for decimals', () => {
    const values = { ...fieldFormValues(), numericPrecision: 20, numericScale: 2 };

    expect(toFieldInput({ ...values, dataType: 'decimal' })).toMatchObject({ numericPrecision: 20, numericScale: 2 });
    expect(toFieldInput({ ...values, dataType: 'int64' })).toMatchObject({ numericPrecision: null, numericScale: null });
  });

  // The API reads null, not '', as "no value" for optional text.
  it('sends empty optional text as null', () => {
    const body = toFieldInput({ ...fieldFormValues(), fieldSourceKind: 'json_path', jsonPath: '  ', unitKey: '' });

    expect(body.jsonPath).toBeNull();
    expect(body.unitKey).toBeNull();
  });

  // The key is fixed once created, so the update body never carries it.
  it('leaves the key out of the update body', () => {
    expect(toFieldUpdateInput({ ...fieldFormValues(), fieldKey: 'region' })).not.toHaveProperty('fieldKey');
  });
});

describe('unusedColumns', () => {
  it('offers typed columns that no column field reads, ignoring case', () => {
    const columns = [column('loan_id', 'string'), column('Branch_Code', 'string'), column('attributes', 'json'), column('sketch', null)];
    const fields = [field('loanId', 'column', 'loan_id'), field('branch', 'column', 'branch_code'), field('region', 'json_path', 'attributes')];

    expect(unusedColumns(columns, fields).map(item => item.name)).toEqual(['attributes']);
  });
});

function column(name: string, dataType: string | null): SourceColumn {
  return { name, columnType: 'varchar(16)', dataType, isNullable: true, isGenerated: false };
}

function field(fieldKey: string, fieldSourceKind: string, sourceColumn: string): Field {
  return {
    fieldId: 1,
    datasetVersionId: 100,
    fieldKey,
    displayName: fieldKey,
    fieldSourceKind,
    sourceColumn,
    dataType: 'string',
    isNullable: true,
    isSelectable: true,
    isFilterable: true,
    isSortable: true,
    isGroupable: false,
    isEnabled: true,
  };
}
