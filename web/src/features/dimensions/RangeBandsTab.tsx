import { GripVertical, Plus, RotateCcw, Save, Trash2 } from 'lucide-react';
import { useMemo } from 'react';
import { FormProvider, useFieldArray, useForm } from 'react-hook-form';
import { Loading } from '@registry/components/Loading';
import { SortableItem, SortableRoot } from '@registry/components/Sortable';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ApiErrorAlert } from '@/shared/ApiErrorAlert';
import {
  DateField,
  DateTimeField,
  EntityField,
  FormReadOnlyContext,
  NumberField,
  SwitchField,
  useProblemSubmit,
} from '@/shared/form';
import type { LookupItem } from '@/shared/types';
import { useLookup } from '@/shared/useLookup';
import { useRangeBands, useReplaceRangeBands } from './api';
import type { RangeBand, RangeBandInput } from './types';

/** The editable bands; dates stay `yyyy-MM-dd` and datetimes ISO UTC strings, as the API takes them. */
interface BandsValues {
  bands: RangeBandInput[];
}

export interface RangeBandsTabProps {
  versionId: number;
  /** The version's `range_data_type`, which picks the bound inputs; null when the version has none yet. */
  rangeDataType: string | null;
  isEditable: boolean;
}

/**
 * The ordered band editor of a range dimension version. A draft or the published version drags bands into order, edits
 * their member, typed bounds and inclusivity, and saves the whole list at once; ordinals follow the order. Overlaps and
 * bound families are checked when the version is validated.
 */
export function RangeBandsTab({ versionId, rangeDataType, isEditable }: RangeBandsTabProps) {
  const bands = useRangeBands(versionId);

  if (bands.isPending) {
    return <Loading size="sm" />;
  }
  if (bands.error) {
    return <ApiErrorAlert error={bands.error} />;
  }
  if (rangeDataType === null) {
    return <p className="text-sm text-muted-foreground">Set the version's range data type in Settings before editing bands.</p>;
  }
  return <RangeBandEditor versionId={versionId} bands={bands.data} rangeDataType={rangeDataType} isEditable={isEditable} />;
}

interface RangeBandEditorProps {
  versionId: number;
  bands: RangeBand[];
  rangeDataType: string;
  isEditable: boolean;
}

function RangeBandEditor({ versionId, bands, rangeDataType, isEditable }: RangeBandEditorProps) {
  const members = useLookup('members', versionId);
  const replace = useReplaceRangeBands(versionId);
  const values = useMemo(() => ({ bands: bands.map(toBandInput) }), [bands]);
  const form = useForm<BandsValues>({ values });
  const { fields, append, remove, move } = useFieldArray({ control: form.control, name: 'bands' });
  const { submit, problem } = useProblemSubmit(form, next =>
    replace.mutateAsync(next.bands.map(band => withFamilyBounds(band, rangeDataType)))
  );

  return (
    <FormProvider {...form}>
      <FormReadOnlyContext value={!isEditable}>
        <form data-slot="range-bands-tab" className="space-y-3" onSubmit={submit}>
          <p className="text-sm text-muted-foreground">
            Bands are evaluated in order, and each must start after the previous one ends. An empty bound is unbounded.
          </p>
          {fields.length === 0 && <p className="py-6 text-center text-sm text-muted-foreground">No bands yet.</p>}
          <SortableRoot
            items={fields.map(field => field.id)}
            onItemsChange={(_, details) => move(details.activeIndex, details.overIndex)}
            activationConstraint={{ distance: 4 }}
          >
            <div className="space-y-2">
              {fields.map((field, index) => (
                <SortableItem key={field.id} id={field.id} index={index} handle disabled={!isEditable}>
                  {({ setNodeRef, setActivatorNodeRef, attributes, activatorListeners, style, isDragging }) => (
                    <div
                      ref={setNodeRef}
                      style={style}
                      data-slot="range-band"
                      className={cn('rounded-md border bg-background p-3', isDragging && 'opacity-60')}
                    >
                      <div className="mb-3 flex items-center gap-2">
                        {isEditable && (
                          <button
                            type="button"
                            ref={setActivatorNodeRef}
                            aria-label={`Drag band ${index + 1}`}
                            className="flex size-6 cursor-grab items-center justify-center rounded-sm text-muted-foreground hover:text-foreground"
                            {...attributes}
                            {...activatorListeners}
                          >
                            <GripVertical className="size-4" />
                          </button>
                        )}
                        <span className="text-sm font-medium">Band {index + 1}</span>
                        {isEditable && (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon-xs"
                            className="ml-auto"
                            aria-label={`Remove band ${index + 1}`}
                            onClick={() => remove(index)}
                          >
                            <Trash2 />
                          </Button>
                        )}
                      </div>
                      <div className="grid gap-4 md:grid-cols-2">
                        <div className="md:col-span-2">
                          <EntityField name={`bands.${index}.memberId`} label="Member" lookupKind="members" parentId={versionId} />
                        </div>
                        <BoundField index={index} side="lower" rangeDataType={rangeDataType} />
                        <BoundField index={index} side="upper" rangeDataType={rangeDataType} />
                        <SwitchField name={`bands.${index}.lowerInclusive`} label="Include the lower bound" />
                        <SwitchField name={`bands.${index}.upperInclusive`} label="Include the upper bound" />
                      </div>
                    </div>
                  )}
                </SortableItem>
              ))}
            </div>
          </SortableRoot>
          <ApiErrorAlert error={problem} />
          {isEditable && (
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!members.data?.length}
                onClick={() => append(nextBand(form.getValues('bands'), members.data ?? []))}
              >
                <Plus />
                Add band
              </Button>
              {members.data?.length === 0 && <span className="text-sm text-muted-foreground">Add members before adding bands.</span>}
              <div className="ml-auto flex items-center gap-2">
                <Button type="button" variant="outline" size="sm" disabled={!form.formState.isDirty} onClick={() => form.reset()}>
                  <RotateCcw />
                  Discard changes
                </Button>
                <Button type="submit" size="sm" disabled={!form.formState.isDirty || replace.isPending}>
                  <Save />
                  Save bands
                </Button>
              </div>
            </div>
          )}
        </form>
      </FormReadOnlyContext>
    </FormProvider>
  );
}

interface BoundFieldProps {
  index: number;
  side: 'lower' | 'upper';
  rangeDataType: string;
}

/** The bound input of the version's range data type: a date, a datetime, or otherwise a decimal. */
function BoundField({ index, side, rangeDataType }: BoundFieldProps) {
  const label = side === 'lower' ? 'Lower bound' : 'Upper bound';
  const description = 'Empty means unbounded.';
  if (rangeDataType === 'date') {
    return <DateField name={`bands.${index}.${side}Date`} label={label} description={description} />;
  }
  if (rangeDataType === 'datetime') {
    return <DateTimeField name={`bands.${index}.${side}Datetime`} label={label} description={description} />;
  }
  return <NumberField name={`bands.${index}.${side}Decimal`} label={label} description={description} />;
}

/** A stored band as the editor holds it: the record without its version and ordinal, with dates cut to `yyyy-MM-dd`. */
function toBandInput(band: RangeBand): RangeBandInput {
  return {
    memberId: band.memberId,
    lowerDecimal: band.lowerDecimal ?? null,
    upperDecimal: band.upperDecimal ?? null,
    lowerDate: band.lowerDate?.slice(0, 10) ?? null,
    upperDate: band.upperDate?.slice(0, 10) ?? null,
    lowerDatetime: band.lowerDatetime ?? null,
    upperDatetime: band.upperDatetime ?? null,
    lowerInclusive: band.lowerInclusive,
    upperInclusive: band.upperInclusive,
  };
}

/** A band as the editor saves it: only the bounds of the family it shows, the other families empty. */
function withFamilyBounds(band: RangeBandInput, rangeDataType: string): RangeBandInput {
  const isDate = rangeDataType === 'date';
  const isDatetime = rangeDataType === 'datetime';
  const isDecimal = !isDate && !isDatetime;
  return {
    ...band,
    lowerDecimal: isDecimal ? band.lowerDecimal : null,
    upperDecimal: isDecimal ? band.upperDecimal : null,
    lowerDate: isDate ? band.lowerDate : null,
    upperDate: isDate ? band.upperDate : null,
    lowerDatetime: isDatetime ? band.lowerDatetime : null,
    upperDatetime: isDatetime ? band.upperDatetime : null,
  };
}

/** A new last band: it starts where the previous one ends and assigns the first member without a band. */
function nextBand(bands: RangeBandInput[], members: LookupItem[]): RangeBandInput {
  const previous = bands.at(-1);
  const assigned = new Set(bands.map(band => band.memberId));
  const member = members.find(item => !assigned.has(item.id)) ?? members[0];
  return {
    memberId: member.id,
    lowerDecimal: previous?.upperDecimal ?? null,
    upperDecimal: null,
    lowerDate: previous?.upperDate ?? null,
    upperDate: null,
    lowerDatetime: previous?.upperDatetime ?? null,
    upperDatetime: null,
    lowerInclusive: true,
    upperInclusive: false,
  };
}
