import { useDimension } from './api';

/** Breadcrumb label of a dimension: its key once loaded. */
export function DimensionCrumb({ dimensionId }: { dimensionId: number }) {
  const dimension = useDimension(dimensionId);
  return <>{dimension.data?.dimensionKey ?? 'Dimension'}</>;
}
