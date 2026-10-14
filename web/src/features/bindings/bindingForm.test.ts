/**
 * Why these tests exist: the binding sheet shows only the references of the chosen kind, the grouping reference of the
 * chosen mode and the time section of time bindings. Values the user picked before switching kind or mode stay in the
 * hidden inputs; sending them would fail API validation on inputs the user can no longer see. They also pin how a
 * stored time extension's JSON periodicities load back into the sheet.
 */
import { describe, expect, it } from 'vitest';
import { bindingFormValues, toBindingInput, type BindingFormValues } from './bindingForm';
import type { BindingDetail } from './types';

describe('toBindingInput', () => {
  const values: BindingFormValues = {
    ...bindingFormValues(),
    displayName: 'Branch',
    dimensionId: 7000,
    sourceFieldId: 1,
    mappingSetId: 30,
    sourceBindingId: 21,
    bridgeId: 40,
    bridgeVersionId: 41,
  };

  it('sends only the references of the chosen kind', () => {
    expect(toBindingInput({ ...values, bindingKind: 'mapped' })).toMatchObject({
      mappingSetId: 30,
      sourceBindingId: null,
      bridgeVersionId: null,
      timeBinding: null,
    });
    expect(toBindingInput({ ...values, bindingKind: 'bridge' })).toMatchObject({
      mappingSetId: null,
      sourceBindingId: 21,
      bridgeVersionId: 41,
    });
    expect(toBindingInput({ ...values, bindingKind: 'direct' })).toMatchObject({ mappingSetId: null, bridgeVersionId: null });
  });

  // The bridge picker only narrows the bridge versions; the API names the bridge version alone.
  it('never sends the bridge', () => {
    expect(toBindingInput({ ...values, bindingKind: 'bridge' })).not.toHaveProperty('bridgeId');
  });

  it('sends only the grouping reference of the chosen mode', () => {
    const grouped = { ...values, groupingLevelId: 501, groupingAncestorMemberId: 601, groupingDepth: 2 };

    expect(toBindingInput({ ...grouped, groupingMode: 'level' })).toMatchObject({
      groupingLevelId: 501,
      groupingAncestorMemberId: null,
      groupingDepth: null,
    });
    expect(toBindingInput({ ...grouped, groupingMode: 'depth' })).toMatchObject({ groupingLevelId: null, groupingDepth: 2 });
    expect(toBindingInput({ ...grouped, groupingMode: 'member' })).toMatchObject({
      groupingLevelId: null,
      groupingAncestorMemberId: null,
      groupingDepth: null,
    });
  });

  it('sends the unknown member only when unmapped rows route to it', () => {
    expect(toBindingInput({ ...values, unmappedBehavior: 'unknown', unknownMemberId: 700 }).unknownMemberId).toBe(700);
    expect(toBindingInput({ ...values, unmappedBehavior: 'exclude', unknownMemberId: 700 }).unknownMemberId).toBeNull();
  });

  it('sends the time section only for time bindings, with the source zone only for timestamps', () => {
    const timed = {
      ...values,
      timeBinding: { ...values.timeBinding, sourceTimeZone: 'America/Chicago', cumulativeFixedStart: '2026-01-01T00:00:00.000Z' },
    };

    expect(toBindingInput({ ...timed, bindingKind: 'direct' }).timeBinding).toBeNull();
    expect(toBindingInput({ ...timed, bindingKind: 'time' }).timeBinding).toMatchObject({
      timeValueType: 'date',
      sourceTimeZone: null,
      cumulativeFixedStart: null,
    });
    const timestamps = { ...timed, bindingKind: 'time', timeBinding: { ...timed.timeBinding, timeValueType: 'timestamp', cumulativeOrigin: 'fixed_start' } };
    expect(toBindingInput(timestamps).timeBinding).toMatchObject({
      sourceTimeZone: 'America/Chicago',
      cumulativeFixedStart: '2026-01-01T00:00:00.000Z',
    });
  });
});

describe('bindingFormValues', () => {
  it('loads a stored time extension, parsing its periodicities and the bridge of its bridge version', () => {
    const detail: BindingDetail = {
      binding: {
        bindingId: 21,
        datasetVersionId: 100,
        displayName: 'As of',
        dimensionId: 7000,
        bindingKind: 'time',
        sourceFieldId: 2,
        groupingMode: 'member',
        raggedBehavior: 'null',
        defaultPeriodAlignment: 'observation',
        unmappedBehavior: 'null',
        isRequired: true,
        isGroupable: true,
        isFilterable: true,
        isEnabled: true,
      },
      timeBinding: {
        bindingId: 21,
        timeValueType: 'timestamp',
        observationKind: 'instant',
        sourceTimeZone: 'Europe/London',
        allowedPeriodicitiesJson: '["day", "month"]',
        defaultQueryTimeZone: 'UTC',
        cumulativeOrigin: 'output_range_start',
      },
      bridgeId: null,
    };

    const loaded = bindingFormValues(detail);

    expect(loaded.timeBinding).toMatchObject({ timeValueType: 'timestamp', sourceTimeZone: 'Europe/London', allowedPeriodicities: ['day', 'month'] });
    expect(loaded).toMatchObject({ isRequired: true, pinnedDimensionVersionId: null, bridgeId: null });
  });
});
