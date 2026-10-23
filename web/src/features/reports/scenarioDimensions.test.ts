/**
 * Why these tests exist: the scenario sheet offers dimensions rather than roles and saves the chosen dimension's one role. A
 * scenario's dimension has exactly one role, which the API requires, so a dimension with several roles is not offered; neither is a
 * dimension whose role is already one of the report's scenarios, which requests would name twice, nor one whose role a calculation
 * dimension of the report reads, which `scenario_roles_distinct` refuses for an enabled report.
 */
import { describe, expect, it } from 'vitest';
import type { LookupItem } from '@/shared/types';
import { scenarioDimensionRoles } from './scenarioDimensions';

function role(id: number, key: string, dimensionId: number): LookupItem {
  return { id, key, label: key, parentId: dimensionId };
}

const roles = [
  role(1, 'planVersion', 610),
  role(2, 'region', 620),
  role(3, 'rateShock', 630),
  role(4, 'strategy', 640),
  role(5, 'priorStrategy', 640),
];

describe('scenarioDimensionRoles', () => {
  it('offers each dimension whose one role is free, mapped to that role', () => {
    const offered = scenarioDimensionRoles(roles, [{ roleId: 1 }], [{ roleId: 2 }]);

    expect([...offered]).toEqual([[630, 3]]);
  });
});
