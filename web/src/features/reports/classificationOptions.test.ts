/**
 * Why these tests exist: a source classifies into the report's dimension, and the API refuses a classification that leads
 * anywhere else or a bridge from a role of another dimension. The source sheet's classification section must offer only what
 * the API accepts: the bridges from the chosen bound role's template, and mapping sets and bridges into the report's dimension.
 * They also pin down that the body carries only the references its kind uses: a bridge its role and no field.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { classificationOptions, toClassificationInput } from './classificationOptions';

function item(id: number, key: string, parentId: number | null = null): LookupItem {
  return { id, key, label: key, parentId };
}

const boundRoles = [item(85, 'account', 800), item(86, 'branch', 600)];

describe('classificationOptions', () => {
  it("offers the bridges of the chosen role's template, and none before a role is chosen", () => {
    expect(classificationOptions(85, 500, boundRoles).bridgeSourceDimensionId).toBe(800);
    expect(classificationOptions(null, 500, boundRoles).bridgeSourceDimensionId).toBeNull();
  });

  it("keeps the mapping sets and bridges that lead into the report's dimension", () => {
    const { leadsIntoTarget } = classificationOptions(null, 500, boundRoles);

    expect([item(62, 'into_target', 500), item(63, 'elsewhere', 600)].filter(leadsIntoTarget).map(set => set.key)).toEqual(['into_target']);
  });
});

describe('toClassificationInput', () => {
  it('sends only the references the kind uses', () => {
    const values = { kind: 'bridge', fieldKey: 'account', roleId: 85, mappingSetId: 62, bridgeId: 95 };

    expect(toClassificationInput(values)).toEqual({ kind: 'bridge', fieldKey: null, roleId: 85, mappingSetId: null, bridgeId: 95 });
    expect(toClassificationInput({ ...values, kind: 'mapped' })).toEqual({
      kind: 'mapped',
      fieldKey: 'account',
      roleId: null,
      mappingSetId: 62,
      bridgeId: null,
    });
  });
});
