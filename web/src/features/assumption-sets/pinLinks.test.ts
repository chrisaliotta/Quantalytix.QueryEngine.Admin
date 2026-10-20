/**
 * Why these tests exist: an assumption set's pins span five kinds of identities, and each pin must open the page of its own
 * identity with the pinned version selected, so a historical set's retired versions can be inspected where they live.
 */
import { describe, expect, it } from 'vitest';
import { pinKindLabel, pinLink } from './pinLinks';
import type { AssumptionPin } from './types';

function pin(kind: string): AssumptionPin {
  return { kind, identityId: 7, identityKey: 'branch', versionId: 71, versionNumber: 2, status: 'retired' };
}

describe('pinLink', () => {
  it("opens each kind's identity page with the pinned version selected", () => {
    expect(pinLink('development', pin('dimension_version'))).toBe('/t/development/dimensions/7?version=71');
    expect(pinLink('development', pin('mapping_version'))).toBe('/t/development/mappings/7?version=71');
    expect(pinLink('development', pin('report_version'))).toBe('/t/development/reports/7?version=71');
    expect(pinKindLabel(pin('calculation_model_version'))).toBe('Calculation model');
  });
});
