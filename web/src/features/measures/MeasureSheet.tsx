import { useMutation } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { invalidateTenantQueries } from '@/api/queryClient';
import { FieldSeparator } from '@/components/ui/field';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import {
  ExpressionField,
  JsonField,
  NumberField,
  SwitchField,
  TextField,
  VocabularyField,
} from '@/shared/form';
import { useLookup } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import { ComponentsField } from './ComponentsField';
import {
  componentRolesFor,
  keepRolesOf,
  measureFormValues,
  sendsTimePolicy,
  toMeasureInput,
  toMeasureUpdateInput,
  usesAggregationFunction,
  type MeasureFormValues,
} from './measureForm';
import { TimePolicyFields } from './TimePolicyFields';
import type { MeasureDetail } from './types';

export interface MeasureSheetProps {
  versionId: number;
  /** The measure to edit; absent for a new measure. */
  detail?: MeasureDetail;
  /** Whether the version is a draft; the sheet is read-only otherwise. */
  isDraft: boolean;
  onClose: () => void;
}

/**
 * Sheet that creates or edits a measure, showing the inputs of the chosen kind: the aggregation function, the
 * components with the roles the kind reads, the formula over the version's measures, or the specialized implementation.
 * Every measure has a row filter over the version's fields, behaviors, output and accumulator sizes, and an optional
 * time policy, which period averages always have. Mounted only while open.
 */
export function MeasureSheet({ versionId, detail, isDraft, onClose }: MeasureSheetProps) {
  const tenantId = useTenantId();
  const form = useForm<MeasureFormValues>({ defaultValues: measureFormValues(detail) });
  const [kind, outputDataType, hasTimePolicy, components] = useWatch({
    control: form.control,
    name: ['measureKind', 'outputDataType', 'hasTimePolicy', 'components'],
  });
  useComponentPruning(form);
  const measures = useLookup('measures', versionId);
  const fields = useLookup('fields', versionId);
  const measureKeys = measures.data?.map(item => item.key).filter(key => key !== detail?.measure.measureKey);
  const fieldKeys = fields.data?.map(item => item.key);
  const roles = componentRolesFor(kind);
  const save = useMutation({
    mutationFn: async (values: MeasureFormValues) => {
      if (detail) {
        return unwrap(
          await apiClient.PUT('/api/tenants/{tenantId}/dataset-versions/{versionId}/measures/{measureId}', {
            params: { path: { tenantId, versionId, measureId: detail.measure.measureId } },
            body: toMeasureUpdateInput(values),
          })
        );
      }
      return unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/dataset-versions/{versionId}/measures', {
          params: { path: { tenantId, versionId } },
          body: toMeasureInput(values),
        })
      );
    },
    onSuccess: () => invalidateTenantQueries(tenantId),
  });

  return (
    <EntityFormSheet
      open
      onOpenChange={open => {
        if (!open) {
          onClose();
        }
      }}
      title={detail?.measure.measureKey ?? 'New measure'}
      description="A backend measure that queries request by key."
      form={form}
      onSubmit={save.mutateAsync}
      readOnly={!isDraft}
      width="44rem"
    >
      <TextField
        name="measureKey"
        label="Key"
        description="The request-facing measure ID, referenced in formulas as [m.key]; fixed once created."
        disabled={detail !== undefined}
      />
      <TextField name="displayName" label="Display name" />
      <VocabularyField name="measureKind" label="Kind" vocabulary="measure_kind" />
      {usesAggregationFunction(kind) && (
        <VocabularyField
          name="aggregationFunction"
          label="Aggregation function"
          vocabulary="aggregation_function"
          allowEmpty={kind === 'specialized'}
        />
      )}
      {kind === 'formula' && (
        <ExpressionField
          name="expressionText"
          label="Formula"
          description="An expression over other measures of this version, such as [m.interest] / [m.balance]."
          measureKeys={measureKeys}
        />
      )}
      {(roles.length > 0 || components.length > 0) && (
        <ComponentsField
          name="components"
          label="Components"
          description="The fields the measure reads, one per role."
          versionId={versionId}
          roles={roles}
        />
      )}
      {kind === 'specialized' && (
        <>
          <TextField name="implementationKey" label="Implementation" description="The allowlisted implementation this measure runs." />
          <TextField name="implementationVersion" label="Implementation version" />
          <JsonField name="configurationJson" label="Configuration" description="Implementation-specific configuration as JSON; empty for none." />
        </>
      )}
      <ExpressionField
        name="rowFilterExpression"
        label="Row filter"
        description="Optional: only source rows this predicate admits count, such as [f.status] = 'open'."
        fieldKeys={fieldKeys}
      />
      <SwitchField name="isEnabled" label="Enabled" description="Disabled measures are not offered to queries." />
      <FieldSeparator>Behaviors</FieldSeparator>
      <VocabularyField name="valueNullBehavior" label="Null values" vocabulary="value_null_behavior" />
      <VocabularyField name="negativeWeightBehavior" label="Negative weights" vocabulary="negative_weight_behavior" />
      <VocabularyField name="emptySetBehavior" label="Empty sets" vocabulary="empty_set_behavior" />
      <VocabularyField name="bridgeFactorBehavior" label="Bridge factor" vocabulary="bridge_factor_behavior" />
      <VocabularyField name="bridgeAllocationBehavior" label="Bridge allocation" vocabulary="bridge_allocation_behavior" />
      <VocabularyField name="totalBehavior" label="Totals" vocabulary="total_behavior" />
      <FieldSeparator>Output</FieldSeparator>
      <VocabularyField name="outputDataType" label="Output type" vocabulary="measure_output_data_type" />
      {outputDataType === 'decimal' && (
        <div className="grid grid-cols-2 gap-4">
          <NumberField name="outputPrecision" label="Precision" />
          <NumberField name="outputScale" label="Scale" />
        </div>
      )}
      <VocabularyField
        name="roundingMode"
        label="Rounding"
        vocabulary="rounding_mode"
        allowEmpty
        description="How clients round the output to its scale; empty when they should not."
      />
      <TextField name="unitKey" label="Unit" description="Optional unit, currency or rate, such as USD or percent." />
      <div className="grid grid-cols-2 gap-4">
        <NumberField
          name="accumulatorPrecision"
          label="Accumulator precision"
          description="The decimal the engine accumulates products in; an overflowing product is null."
        />
        <NumberField name="accumulatorScale" label="Accumulator scale" />
      </div>
      <FieldSeparator>Time policy</FieldSeparator>
      {kind !== 'period_average' && (
        <SwitchField
          name="hasTimePolicy"
          label="Time policy"
          description="Select observations per series and period, or weigh intervals by their duration."
        />
      )}
      {sendsTimePolicy({ measureKind: kind, hasTimePolicy }) && <TimePolicyFields versionId={versionId} />}
    </EntityFormSheet>
  );
}

/** Drops the components whose role a newly chosen kind does not read, so the editor shows exactly what is sent. */
function useComponentPruning(form: UseFormReturn<MeasureFormValues>) {
  useEffect(() => {
    const subscription = form.watch((values, { name }) => {
      if (name === 'measureKind') {
        form.setValue('components', keepRolesOf(values.measureKind ?? null, form.getValues('components')));
      }
    });
    return () => subscription.unsubscribe();
  }, [form]);
}
