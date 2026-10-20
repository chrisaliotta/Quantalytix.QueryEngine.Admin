import { useMutation, useQuery, type QueryKey } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import type { LookupKind } from '@/shared/types';
import { lookupQueryKey } from '@/shared/useLookup';
import { useTenantId } from '@/shared/useTenantId';
import type {
  CalculationModelInput,
  ModelContentInput,
  ModelContentItem,
  ModelContentKind,
  ModelVersionInput,
  NewCalculationModelInput,
  NewModelVersionInput,
} from './types';

/** The list paths of the content kinds. */
type ContentListPath = `/api/tenants/{tenantId}/model-versions/{versionId}/${ModelContentKind}`;

/** The item paths of the content kinds. */
type ContentItemPath = `${ContentListPath}/{itemId}`;

/** The pickers that list model version content; a content write can change any of them, through cascades. */
const CONTENT_LOOKUPS: LookupKind[] = [
  'variables',
  'variable-sources',
  'source-profiles',
  'calculated-measures',
  'calculation-dimensions',
];

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function modelsKey(tenantId: string) {
  return ['calculation-models', tenantId] as const;
}

function versionKey(tenantId: string, versionId: number) {
  return ['model-versions', tenantId, versionId] as const;
}

function contentKey(tenantId: string, versionId: number, kind?: ModelContentKind) {
  return kind ? (['model-content', tenantId, versionId, kind] as const) : (['model-content', tenantId, versionId] as const);
}

/** Marks the given queries stale. */
function invalidate(...queryKeys: QueryKey[]) {
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Every calculation model of the current tenant, by key. */
export function useCalculationModels() {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: modelsKey(tenantId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/api/tenants/{tenantId}/calculation-models', { params: { path: { tenantId } } })),
  });
}

/** One calculation model, with its current-version pointer. */
export function useCalculationModel(modelId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: [...modelsKey(tenantId), modelId],
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
        })
      ),
  });
}

/** One model version; `null` keeps the query idle until a version is selected. */
export function useModelVersion(versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: versionKey(tenantId, versionId ?? 0),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId: versionId as number } },
        })
      ),
    enabled: versionId !== null,
  });
}

/** Creates a calculation model without versions. */
export function useCreateCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewCalculationModelInput) =>
      unwrap(await apiClient.POST('/api/tenants/{tenantId}/calculation-models', { params: { path: { tenantId } }, body })),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Edits a calculation model's name and availability. */
export function useUpdateCalculationModel(modelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: CalculationModelInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
          body,
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'calculation-models')),
  });
}

/** Deletes a model whose versions are all drafts and that no report version reads; a 409 names what blocks it. */
export function useDeleteCalculationModel() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (modelId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/calculation-models/{modelId}', {
          params: { path: { tenantId, modelId } },
        })
      ),
    onSuccess: () =>
      invalidate(
        modelsKey(tenantId),
        lookupQueryKey(tenantId, 'calculation-models'),
        lookupQueryKey(tenantId, 'model-versions')
      ),
  });
}

/** Creates a draft model version, empty or cloned with all its content. */
export function useCreateModelVersion(modelId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: NewModelVersionInput) =>
      unwrap(
        await apiClient.POST('/api/tenants/{tenantId}/calculation-models/{modelId}/versions', {
          params: { path: { tenantId, modelId } },
          body,
        })
      ),
    onSuccess: () => invalidate(lookupQueryKey(tenantId, 'model-versions')),
  });
}

/** Edits a draft or published version's currency. */
export function useUpdateModelVersion(versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ModelVersionInput) =>
      unwrap(
        await apiClient.PUT('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
          body,
        })
      ),
    onSuccess: () => invalidate(versionKey(tenantId, versionId), lookupQueryKey(tenantId, 'model-versions')),
  });
}

/** Deletes a draft version with its content. */
export function useDeleteModelVersion() {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (versionId: number) =>
      unwrap(
        await apiClient.DELETE('/api/tenants/{tenantId}/model-versions/{versionId}', {
          params: { path: { tenantId, versionId } },
        })
      ),
    onSuccess: () => invalidate(modelsKey(tenantId), lookupQueryKey(tenantId, 'model-versions')),
  });
}

/** The rows of one content kind of a model version, by key. */
export function useModelContent<K extends ModelContentKind>(kind: K, versionId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: contentKey(tenantId, versionId, kind),
    queryFn: async () => {
      const path = `/api/tenants/{tenantId}/model-versions/{versionId}/${kind}` as ContentListPath;
      const rows = unwrap(await apiClient.GET(path, { params: { path: { tenantId, versionId } } }));
      return rows as ModelContentItem<K>[];
    },
  });
}

/**
 * Refreshes every content kind of the version and the content pickers after a write, because deletes cascade: removing
 * a variable also removes its sources, their picks and their override routes.
 */
function invalidateContent(tenantId: string, versionId: number) {
  return invalidate(contentKey(tenantId, versionId), ...CONTENT_LOOKUPS.map(kind => lookupQueryKey(tenantId, kind)));
}

/** Adds a row of one content kind to a draft or published model version. */
export function useCreateModelContent<K extends ModelContentKind>(kind: K, versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (body: ModelContentInput<K>) => {
      const path = `/api/tenants/{tenantId}/model-versions/{versionId}/${kind}` as ContentListPath;
      return unwrap(await apiClient.POST(path, { params: { path: { tenantId, versionId } }, body })) as ModelContentItem<K>;
    },
    onSuccess: () => invalidateContent(tenantId, versionId),
  });
}

/** Replaces a row of one content kind of a draft or published model version, keeping its ID. */
export function useUpdateModelContent<K extends ModelContentKind>(kind: K, versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async ({ itemId, body }: { itemId: number; body: ModelContentInput<K> }) => {
      const path = `/api/tenants/{tenantId}/model-versions/{versionId}/${kind}/{itemId}` as ContentItemPath;
      return unwrap(
        await apiClient.PUT(path, { params: { path: { tenantId, versionId, itemId } }, body })
      ) as ModelContentItem<K>;
    },
    onSuccess: () => invalidateContent(tenantId, versionId),
  });
}

/** Deletes a row of one content kind with the rows only it gives meaning to; a 409 names the report content using it. */
export function useDeleteModelContent(kind: ModelContentKind, versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (itemId: number) => {
      const path = `/api/tenants/{tenantId}/model-versions/{versionId}/${kind}/{itemId}` as ContentItemPath;
      return unwrap(await apiClient.DELETE(path, { params: { path: { tenantId, versionId, itemId } } }));
    },
    onSuccess: () => invalidateContent(tenantId, versionId),
  });
}

/** Saves a sheet's row: creates it when `itemId` is null, otherwise replaces the row with that ID. */
export function useSaveModelContent<K extends ModelContentKind>(kind: K, versionId: number, itemId: number | null) {
  const create = useCreateModelContent(kind, versionId);
  const update = useUpdateModelContent(kind, versionId);
  return (body: ModelContentInput<K>) => (itemId === null ? create.mutateAsync(body) : update.mutateAsync({ itemId, body }));
}
