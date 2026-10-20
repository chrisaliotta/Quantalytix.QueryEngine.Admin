/**
 * Why these tests exist: a source classifies into the report's dimension and a route into its calculation dimension's, and
 * the API refuses a classification that leads anywhere else when it is saved. The source and route sheets share one
 * classification section whose pickers must offer only what the API accepts: every field for a direct or mapped
 * classification but only member-grouped fields for a bridge, bridges from the chosen field's dimension, and mapping sets
 * and bridges into the target. They also pin down that the body carries only the reference its kind uses.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { classificationOptions, toClassificationInput } from './classificationOptions';

function item(id: number, key: string, parentId: number | null = null): LookupItem {
  return { id, key, label: key, parentId };
}

const fields = [item(1, 'account'), item(2, 'branchKey'), item(3, 'memo')];
const classifiedFields = [item(1, 'account', 800)];

describe('classificationOptions', () => {
  it('offers every field to a direct or mapped classification, and only member-grouped fields to a bridge', () => {
    expect(classificationOptions({ kind: 'mapped', fieldKey: null }, 500, fields, classifiedFields).fields).toBe(fields);
    expect(classificationOptions({ kind: 'bridge', fieldKey: null }, 500, fields, classifiedFields).fields).toBe(classifiedFields);
  });

  it("offers the bridges of the chosen field's dimension, and none before a member-grouped field is chosen", () => {
    expect(classificationOptions({ kind: 'bridge', fieldKey: 'account' }, 500, fields, classifiedFields).bridgeSourceDimensionId).toBe(800);
    expect(classificationOptions({ kind: 'bridge', fieldKey: null }, 500, fields, classifiedFields).bridgeSourceDimensionId).toBeNull();
  });

  it('keeps the mapping sets and bridges that lead into the target', () => {
    const { leadsIntoTarget } = classificationOptions({ kind: 'mapped', fieldKey: 'branchKey' }, 500, fields, classifiedFields);

    expect([item(62, 'into_target', 500), item(63, 'elsewhere', 600)].filter(leadsIntoTarget).map(set => set.key)).toEqual(['into_target']);
  });
});

describe('toClassificationInput', () => {
  it('sends only the reference the kind uses', () => {
    const values = { kind: 'bridge', fieldKey: 'account', mappingSetId: 62, bridgeId: 95 };

    expect(toClassificationInput(values)).toEqual({ kind: 'bridge', fieldKey: 'account', mappingSetId: null, bridgeId: 95 });
    expect(toClassificationInput({ ...values, kind: 'direct' })).toEqual({
      kind: 'direct',
      fieldKey: 'account',
      mappingSetId: null,
      bridgeId: null,
    });
  });
});
