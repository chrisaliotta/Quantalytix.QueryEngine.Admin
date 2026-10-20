import { useAssumptionSet } from './useAssumptionSets';

/** Breadcrumb of an assumption set page: the set's key once it has loaded. */
export function AssumptionSetCrumb({ assumptionSetId }: { assumptionSetId: number }) {
  const detail = useAssumptionSet(assumptionSetId);
  return <>{detail.data?.set.assumptionSetKey ?? 'Assumption set'}</>;
}
