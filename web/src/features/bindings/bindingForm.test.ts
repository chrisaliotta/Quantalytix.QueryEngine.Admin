/**
 * Why these tests exist: the binding sheet shows only the references of the chosen kind, a role for a breakout binding or a
 * dimension for a time binding, and the time section of time bindings. Values the user picked before switching kind stay in
 * the hidden inputs; sending them would fail API validation on inputs the user can no longer see. They also pin how a stored
 * time extension's JSON periodicities load back into the sheet, and that a role binding's stored dimension, its role's
 * template, does not load as a time dimension.
 */
import { describe, expect, it } from 'vitest';
import { bindingFormValues, toBindingInput, type BindingFormValues } from './bindingForm';
import type { BindingDetail } from './types';

describe('toBindingInput', () => {
  const values: BindingFormValues = {
    ...bindingFormValues(),
    dimensionRoleId: 81,
    dimensionId: 7000,
    sourceFieldId: 1,
    mappingSetId: 30,
    sourceDimensionBindingId: 21,
    bridgeId: 40,
  };

  it('sends only the references of the chosen kind', () => {
    expect(toBindingInput({ ...values, bindingKind: 'mapped' })).toMatchObject({
      mappingSetId: 30,
      sourceDimensionBindingId: null,
      bridgeId: null,
      timeBinding: null,
    });
    expect(toBindingInput({ ...values, bindingKind: 'bridge' })).toMatchObject({
      mappingSetId: null,
      sourceDimensionBindingId: 21,
      bridgeId: 40,
    });
    expect(toBindingInput({ ...values, bindingKind: 'direct' })).toMatchObject({ mappingSetId: null, bridgeId: null });
  });

  it('sends the role of a breakout binding and the dimension of a time binding', () => {
    expect(toBindingInput({ ...values, bindingKind: 'direct' })).toMatchObject({ dimensionRoleId: 81, dimensionId: null });
    expect(toBindingInput({ ...values, bindingKind: 'time' })).toMatchObject({ dimensionRoleId: null, dimensionId: 7000 });
  });

  it('sends the unknown member only when unmapped rows route to it', () => {
    expect(toBindingInput({ ...values, unmappedBehavior: 'unknown', unknownMemberKey: 'UNKNOWN' }).unknownMemberKey).toBe('UNKNOWN');
    expect(toBindingInput({ ...values, unmappedBehavior: 'exclude', unknownMemberKey: 'UNKNOWN' }).unknownMemberKey).toBeNull();
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
  const binding: BindingDetail['binding'] = {
    dimensionBindingId: 21,
    datasetVersionId: 100,
    dimensionId: 7000,
    bindingKind: 'time',
    sourceFieldId: 2,
    raggedBehavior: 'null',
    defaultPeriodAlignment: 'observation',
    unmappedBehavior: 'null',
    isRequired: true,
    isGroupable: true,
    isFilterable: true,
    isEnabled: true,
  };

  it('loads a stored time extension, parsing its periodicities', () => {
    const detail: BindingDetail = {
      binding,
      timeBinding: {
        dimensionBindingId: 21,
        timeValueType: 'timestamp',
        observationKind: 'instant',
        sourceTimeZone: 'Europe/London',
        allowedPeriodicitiesJson: '["day", "month"]',
        defaultQueryTimeZone: 'UTC',
        cumulativeOrigin: 'output_range_start',
      },
    };

    const loaded = bindingFormValues(detail);

    expect(loaded.timeBinding).toMatchObject({ timeValueType: 'timestamp', sourceTimeZone: 'Europe/London', allowedPeriodicities: ['day', 'month'] });
    expect(loaded).toMatchObject({ isRequired: true, bridgeId: null, dimensionRoleId: null, dimensionId: 7000 });
  });

  it("loads a role binding by its role, leaving the template out of the time dimension", () => {
    const loaded = bindingFormValues({ binding: { ...binding, bindingKind: 'direct', dimensionRoleId: 81 } });

    expect(loaded).toMatchObject({ dimensionRoleId: 81, dimensionId: null });
  });
});
