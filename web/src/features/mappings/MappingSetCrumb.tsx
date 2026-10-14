import { useMappingSet } from './api';

/** Breadcrumb label of a mapping set: its key once loaded. */
export function MappingSetCrumb({ mappingSetId }: { mappingSetId: number }) {
  const mappingSet = useMappingSet(mappingSetId);
  return <>{mappingSet.data?.mappingSetKey ?? 'Mapping set'}</>;
}
