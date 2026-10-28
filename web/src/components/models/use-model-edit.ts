import { useCallback } from 'react'
import type { ApiResult, ModelsApi, Problem, Violation } from '@/lib/models/contract'
import { useModelsApi, useRefreshModels } from '@/lib/models/client'

/** What a refused call shows: the rules it breaks or the blockers, else each input error, else its summary. */
export function refusalOf(problem: Problem): Violation[] {
  if (problem.violations?.length) return problem.violations
  const errors = Object.entries(problem.errors).flatMap(([path, messages]) => messages.map((message) => ({ code: path || 'invalid_input', message })))
  return errors.length ? errors : [{ code: 'refused', message: problem.detail ?? problem.title }]
}

export type RunEdit = (call: (api: ModelsApi) => Promise<ApiResult<unknown>>) => Promise<Violation[] | null>

/** Runs a port call against the surrounding (staged) `ModelsApi`; refreshes on success, or answers the server's refusal. */
export function useModelEdit(): RunEdit {
  const api = useModelsApi()
  const refresh = useRefreshModels()
  return useCallback(
    async (call) => {
      const result = await call(api)
      if (!result.ok) return refusalOf(result.problem)
      await refresh()
      return null
    },
    [api, refresh],
  )
}
