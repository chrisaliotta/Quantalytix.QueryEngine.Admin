import { useDataset } from './queries';

/** Breadcrumb of a dataset page: the dataset's key once it has loaded. */
export function DatasetCrumb({ datasetId }: { datasetId: number }) {
  const dataset = useDataset(datasetId);
  return <>{dataset.data?.datasetKey ?? 'Dataset'}</>;
}
