import { useCalculationModel } from './api';

/** Breadcrumb label of a calculation model: its key once loaded. */
export function CalculationModelCrumb({ calculationModelId }: { calculationModelId: number }) {
  const model = useCalculationModel(calculationModelId);
  return <>{model.data?.modelKey ?? 'Calculation model'}</>;
}
