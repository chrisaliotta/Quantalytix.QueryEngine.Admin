import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { ExpressionField, KeyField, MultiKeyField, TextField } from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useDeleteAttribution, useReplaceAttribution } from './api';
import { attributionInputs, toAttributionInput, toAttributionValues, type AttributionValues } from './attributionForm';
import type { MetricAttribution } from './types';

/** What the driver pickers call the days inputs. */
const DAYS_LABELS: Record<string, string> = { '@periodDays': 'Days in the period', '@yearDays': 'Days in its year' };

export interface AttributionSheetProps {
  calculationModelId: number;
  /** The variable or calculated measure attributed. */
  metricKey: string;
  /** The calculated measure's formula, which an empty expression switches the drivers in; null for a variable, which has none. */
  formula: string | null;
  /** The metric's stored attribution; undefined when it has none yet. */
  attribution: MetricAttribution | undefined;
  onClose: () => void;
}

/**
 * Edits how a metric's change between two values, such as a month and the month before or actuals and a budget, splits into the
 * effects of its drivers: the expression they are switched in, the drivers in the order sequential attribution switches them, and
 * the variable that weighs member contributions. Saving replaces the metric's attribution whole; the model's rules are checked
 * before anything is written.
 */
export function AttributionSheet({ calculationModelId, metricKey, formula, attribution, onClose }: AttributionSheetProps) {
  const replace = useReplaceAttribution(calculationModelId, metricKey);
  const remove = useDeleteAttribution(calculationModelId, metricKey);
  const variables = useLookup('variables', calculationModelId);
  const measures = useLookup('calculated-measures', calculationModelId);
  const form = useForm<AttributionValues>({ defaultValues: toAttributionValues(attribution) });
  const drivers = useFieldArray({ control: form.control, name: 'drivers' });
  const [expressionText, chosen] = useWatch({ control: form.control, name: ['expressionText', 'drivers'] });
  const metrics = variables.data && measures.data ? [...variables.data, ...measures.data] : null;
  const labels = new Map(metrics?.map(item => [item.key, item.label]));
  const offered = new Set([...attributionInputs(expressionText.trim() || (formula ?? '')), ...chosen.flatMap(driver => driver.inputKeys)]);
  const inputs: LookupItem[] | null =
    metrics && [...offered].map((key, index) => ({ id: index, key, label: DAYS_LABELS[key] ?? labels.get(key) ?? key }));

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={`Attribution of ${metricKey}`}
      description="Without drivers of its own, each metric the expression reads is a driver, and @periodDays with @yearDays form one days driver, which YEARFRAC reads too. Parameters are never drivers. The residual is the change the drivers leave unexplained."
      form={form}
      onSubmit={values => replace.mutateAsync(toAttributionInput(values))}
      width="44rem"
    >
      <ExpressionField
        name="expressionText"
        label="Expression"
        measureKeys={metrics?.map(item => item.key)}
        description={
          formula === null
            ? 'Required, because a variable has no formula: an identity over other metrics, such as [m.AVERAGE_BALANCE] * [m.YIELD] * @periodDays / @yearDays.'
            : `Optional; empty switches the drivers in the formula, ${formula}.`
        }
      />
      <KeyField
        name="weightVariableKey"
        label="Weight variable"
        items={variables.data ?? null}
        allowEmpty
        description="Optional: the variable that weighs child rows and members when a ratio's change splits into rate, mix and interaction, such as the average balance for a yield."
      />
      <section data-slot="attribution-drivers" className="space-y-2">
        <div className="text-sm font-medium">Drivers</div>
        <p className="text-sm text-muted-foreground">
          Each input of the expression belongs to exactly one driver; a metric has at most eight. Sequential attribution switches
          them in this order.
        </p>
        {drivers.fields.map((field, index) => (
          <div key={field.id} data-slot="attribution-driver" className="space-y-3 rounded-md border p-3">
            <div className="flex items-center gap-1">
              <span className="text-sm font-medium">Driver {index + 1}</span>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                className="ml-auto"
                aria-label={`Move driver ${index + 1} up`}
                disabled={index === 0}
                onClick={() => drivers.move(index, index - 1)}
              >
                <ArrowUp />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                aria-label={`Move driver ${index + 1} down`}
                disabled={index === drivers.fields.length - 1}
                onClick={() => drivers.move(index, index + 1)}
              >
                <ArrowDown />
              </Button>
              <Button type="button" variant="ghost" size="icon-xs" aria-label={`Remove driver ${index + 1}`} onClick={() => drivers.remove(index)}>
                <Trash2 />
              </Button>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <TextField name={`drivers.${index}.driverKey`} label="Key" description="Names the driver's report column; never residual." />
              <TextField name={`drivers.${index}.displayName`} label="Name" />
            </div>
            <MultiKeyField
              name={`drivers.${index}.inputKeys`}
              label="Inputs"
              items={inputs}
              description="The inputs of the expression that switch together: its metrics, and its days when it reads them or calls YEARFRAC."
            />
          </div>
        ))}
        <Button type="button" variant="outline" size="sm" onClick={() => drivers.append({ driverKey: '', displayName: '', inputKeys: [] })}>
          <Plus />
          Add driver
        </Button>
      </section>
      {attribution && (
        <div className="space-y-2 border-t pt-4">
          <ApiErrorAlert error={remove.error} />
          <Button type="button" variant="outline" className="text-destructive" onClick={() => remove.mutate(undefined, { onSuccess: onClose })}>
            <Trash2 />
            Remove attribution
          </Button>
        </div>
      )}
    </EntityFormSheet>
  );
}
