/**
 * Why these tests exist: an assumption set's pins span three kinds of identities, and each pin must open the page where its
 * identity lives with the pinned version selected, so a historical set's retired versions can be inspected there. Mapping sets and
 * bridges live under their dimension, and what a report owns lives in its report's editor.
 */
import { describe, expect, it } from 'vitest';
import { pinLink } from './pinLinks';
import type { AssumptionPin } from './types';

function pin(kind: string, reportId: number | null = null): AssumptionPin {
  return { kind, identityId: 7, identityKey: 'branch', dimensionId: 3, reportId, versionId: 71, versionNumber: 2, status: 'retired' };
}

describe('pinLink', () => {
  it("opens each kind's identity page under its dimension with the pinned version selected", () => {
    expect(pinLink('development', pin('dimension_version'))).toBe('/t/development/dimensions/3?version=71');
    expect(pinLink('development', pin('mapping_version'))).toBe('/t/development/dimensions/3/mapping-sets/7?version=71');
    expect(pinLink('development', pin('bridge_version'))).toBe('/t/development/dimensions/3/bridges/7?version=71');
  });

  it("opens the report's editor for a report's dimension and the bridges into it", () => {
    expect(pinLink('development', pin('dimension_version', 60))).toBe('/t/development/reports/60');
    expect(pinLink('development', pin('bridge_version', 60))).toBe('/t/development/reports/60');
  });
});
