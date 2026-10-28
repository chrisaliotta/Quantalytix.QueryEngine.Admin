import { createContext, useCallback, useContext, useMemo, type ReactNode } from 'react'
import useSWR from 'swr'
import { unwrap } from '@/lib/api/contract'
import { sharedModelsApi } from '@/lib/models/client'
import type { ModelDocument } from '@/lib/models/contract'
import type { DisplayFormat } from '@/lib/report/types'
import type { CalcModel, Rounding } from './types'

/**
 * Compatibility view for the report editor, which still reads models in the prototype's shape: every model of the
 * `ModelsApi`, read-only. The model screens edit through `lib/models`; their refresh revalidates this view too.
 */

interface ModelsContextValue {
  models: CalcModel[]
  getModel: (key: string) => CalcModel | undefined
}

const ModelsContext = createContext<ModelsContextValue | null>(null)

function toLegacy(doc: ModelDocument): CalcModel {
  const attribution = (key: string) => {
    const a = doc.attributions.find((x) => x.metricKey === key)
    if (!a) return undefined
    return {
      expression: a.expression ?? undefined,
      weight: a.weightVariableKey ?? undefined,
      useDefault: a.drivers.length === 0,
      drivers: a.drivers.map((d) => ({ key: d.key, name: d.displayName, inputs: d.inputs })),
    }
  }
  return {
    key: doc.key,
    name: doc.displayName,
    currency: doc.currencyCode,
    calendar: doc.calendarKey,
    language: doc.formulaLanguage,
    method: doc.attributionMethod,
    enabled: doc.isEnabled,
    parameters: doc.parameters.map((p) => ({
      key: p.key,
      name: p.displayName,
      type: p.valueType,
      allowedValues: p.allowedValues ?? [],
      defaultNumber: p.defaultNumber,
      defaultText: p.defaultText,
      description: p.description ?? '',
    })),
    variables: doc.variables.map((v) => ({
      key: v.key,
      name: v.displayName,
      format: v.displayFormat as DisplayFormat,
      unit: v.unitKey ?? '',
      missing: v.missingBehavior,
      attribution: attribution(v.key),
    })),
    measures: doc.measures.map((m) => ({
      key: m.key,
      name: m.displayName,
      formula: m.formula,
      precision: m.outputPrecision ?? 28,
      scale: m.outputScale ?? 0,
      rounding: (m.roundingMode ?? 'half_even') as Rounding,
      unit: m.unitKey ?? '',
      format: m.displayFormat as DisplayFormat,
      attribution: attribution(m.key),
    })),
  }
}

async function loadModels() {
  const api = sharedModelsApi()
  const list = await api.listModels()
  return Promise.all(list.map((m) => unwrap(api.getModel(m.id))))
}

export function ModelsProvider({ children }: { children: ReactNode }) {
  const { data } = useSWR(['models', 'compat'], loadModels, { revalidateOnFocus: false })
  const models = useMemo(() => (data ?? []).map(toLegacy), [data])
  const getModel = useCallback((key: string) => models.find((m) => m.key === key), [models])
  const value = useMemo(() => ({ models, getModel }), [models, getModel])
  return <ModelsContext.Provider value={value}>{children}</ModelsContext.Provider>
}

export function useModels() {
  const ctx = useContext(ModelsContext)
  if (!ctx) throw new Error('useModels must be used inside ModelsProvider')
  return ctx
}
