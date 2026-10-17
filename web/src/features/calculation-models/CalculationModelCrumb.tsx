import { useCalculationModel } from './api';

/** Breadcrumb label of a calculation model: its key once loaded. */
export function CalculationModelCrumb({ modelId }: { modelId: number }) {
  const model = useCalculationModel(modelId);
  return <>{model.data?.modelKey ?? 'Calculation model'}</>;
}
