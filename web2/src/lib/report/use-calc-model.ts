import { useModels } from '@/lib/model/store'
import { useEditor } from './store'

/**
 * The variables and measures a report works with come from its bound calculation model.
 * The report's own copies are only a fallback for a report whose model is missing.
 */
export function useCalcModel() {
  const { report } = useEditor()
  const { getModel } = useModels()
  const model = getModel(report.settings.model)
  return {
    model,
    variables: model?.variables ?? report.variables,
    measures: model?.measures ?? report.measures,
    parameters: model?.language === '1.2' ? model.parameters : [],
  }
}
