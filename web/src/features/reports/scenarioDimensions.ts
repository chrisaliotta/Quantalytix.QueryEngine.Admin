import type { LookupItem } from '@/shared/types';
import type { CalculationDimension, ReportScenario } from './types';

/**
 * The dimensions the scenario sheet offers, each with the one role a scenario of it names. A scenario's dimension has exactly one
 * role, which the API requires, so the sheet offers dimensions and saves the chosen dimension's role. It leaves out a dimension with
 * several roles; one whose role is a scenario of the report already, because requests name each scenario by its role's key; and one
 * whose role a calculation dimension of the report reads, because a scenario's member is chosen per run, never broken out or
 * filtered by.
 */
export function scenarioDimensionRoles(
  roles: LookupItem[],
  scenarios: Pick<ReportScenario, 'roleId'>[],
  dimensions: Pick<CalculationDimension, 'roleId'>[]
): Map<number, number> {
  const taken = new Set([...scenarios, ...dimensions].map(item => item.roleId));
  const rolesOf = new Map<number, LookupItem[]>();
  for (const role of roles) rolesOf.set(role.parentId!, [...(rolesOf.get(role.parentId!) ?? []), role]);
  return new Map(
    [...rolesOf].filter(([, own]) => own.length === 1 && !taken.has(own[0].id)).map(([dimensionId, [role]]) => [dimensionId, role.id])
  );
}
