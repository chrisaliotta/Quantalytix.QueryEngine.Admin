import { queryOptions, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';

const vocabulariesQuery = queryOptions({
  queryKey: ['vocabularies'],
  queryFn: async () => unwrap(await apiClient.GET('/api/vocabularies')),
  staleTime: Infinity,
});

/** All controlled vocabularies; loaded once, they only change with an API deployment. */
export function useVocabularies() {
  return useQuery(vocabulariesQuery);
}

/** One vocabulary by name, e.g. `useVocabulary('data_type')`; `data` is undefined until loaded. */
export function useVocabulary(name: string) {
  return useQuery({
    ...vocabulariesQuery,
    select: vocabularies => vocabularies.find(vocabulary => vocabulary.name === name),
  });
}
