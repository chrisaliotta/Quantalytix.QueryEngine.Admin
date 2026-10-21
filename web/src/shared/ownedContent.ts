import { useMutation, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { unwrap } from '@/api/problemDetails';
import { queryClient } from '@/api/queryClient';
import type { components } from '@/api/schema';
import type { LookupKind } from './types';
import { lookupQueryKey } from './useLookup';
import { useTenantId } from './useTenantId';

type Schemas = components['schemas'];

/**
 * The content the API serves row by row under an owner, by the route segment of its kind: the owner resource it belongs to,
 * its row and its create and edit body. A calculation model holds the metrics; a report the profiles and calculation
 * dimensions it reads them through.
 */
export interface OwnedContentTypes {
  variables: { scope: 'calculation-models'; item: Schemas['VariableRecord']; input: Schemas['VariableInput'] };
  'calculated-measures': {
    scope: 'calculation-models';
    item: Schemas['CalculatedMeasureRecord'];
    input: Schemas['CalculatedMeasureInput'];
  };
  profiles: { scope: 'reports'; item: Schemas['SourceProfile']; input: Schemas['SourceProfileInput'] };
  'calculation-dimensions': {
    scope: 'reports';
    item: Schemas['CalculationDimensionRecord'];
    input: Schemas['CalculationDimensionInput'];
  };
}

/** A kind of owned content. */
export type ContentKind = keyof OwnedContentTypes;

/** A row of one content kind. */
export type ContentItem<K extends ContentKind> = OwnedContentTypes[K]['item'];

/** The create and edit body of one content kind. */
export type ContentInput<K extends ContentKind> = OwnedContentTypes[K]['input'];

/** An owner resource whose content the API serves row by row. */
type ContentScope = OwnedContentTypes[ContentKind]['scope'];

/** The route parameter each owner resource's routes name it by. */
const OWNER_PARAMS = { 'calculation-models': 'modelId', reports: 'reportId' } as const satisfies Record<ContentScope, string>;

/** The list paths of the content kinds. */
type ContentListPath = {
  [K in ContentKind]: `/api/tenants/{tenantId}/${OwnedContentTypes[K]['scope']}/{${(typeof OWNER_PARAMS)[OwnedContentTypes[K]['scope']]}}/${K}`;
}[ContentKind];

/** The item paths of the content kinds. */
type ContentItemPath = `${ContentListPath}/{itemId}`;

/** The path parameters of a content route; each route names its owner by one of the two, so the union of routes takes both. */
type ContentPathParams = { tenantId: string; modelId: number; reportId: number; itemId: number };

/** The owner resource each kind's routes start with. */
const SCOPES: { [K in ContentKind]: OwnedContentTypes[K]['scope'] } = {
  variables: 'calculation-models',
  'calculated-measures': 'calculation-models',
  profiles: 'reports',
  'calculation-dimensions': 'reports',
};

/** The pickers that list each owner resource's content; a content write can change any of them, through cascades. */
const CONTENT_LOOKUPS: Record<ContentScope, LookupKind[]> = {
  'calculation-models': ['variables', 'calculated-measures'],
  reports: ['report-sources', 'source-profiles', 'calculation-dimensions'],
};

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function contentKey(tenantId: string, scope: ContentScope, ownerId: number, kind?: ContentKind) {
  return kind
    ? (['owned-content', tenantId, scope, ownerId, kind] as const)
    : (['owned-content', tenantId, scope, ownerId] as const);
}

function listPath(kind: ContentKind) {
  const scope = SCOPES[kind];
  return `/api/tenants/{tenantId}/${scope}/{${OWNER_PARAMS[scope]}}/${kind}` as ContentListPath;
}

function itemPath(kind: ContentKind) {
  return `${listPath(kind)}/{itemId}` as ContentItemPath;
}

/** The path parameters of a kind's routes, naming the owner by the parameter its scope uses. */
function pathParams(kind: ContentKind, tenantId: string, ownerId: number, itemId?: number) {
  return { tenantId, [OWNER_PARAMS[SCOPES[kind]]]: ownerId, itemId } as ContentPathParams;
}

/** The rows of one content kind of an owner, by key. */
export function useOwnedContent<K extends ContentKind>(kind: K, ownerId: number) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: contentKey(tenantId, SCOPES[kind], ownerId, kind),
    queryFn: async () => {
      const rows = unwrap(await apiClient.GET(listPath(kind), { params: { path: pathParams(kind, tenantId, ownerId) } }));
      return rows as ContentItem<K>[];
    },
  });
}

/**
 * Refreshes every content kind of the owner and its pickers after a write, because deletes cascade: removing a report source
 * also removes its profile picks. The report editor's source writes call it too, because they write sources and profile picks.
 */
export function invalidateOwnedContent(tenantId: string, scope: ContentScope, ownerId: number) {
  const queryKeys = [contentKey(tenantId, scope, ownerId), ...CONTENT_LOOKUPS[scope].map(lookup => lookupQueryKey(tenantId, lookup))];
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Saves a sheet's row: creates it when `itemId` is null, otherwise replaces the row with that ID. */
export function useSaveOwnedContent<K extends ContentKind>(kind: K, ownerId: number, itemId: number | null) {
  const tenantId = useTenantId();
  const save = useMutation({
    mutationFn: async (body: ContentInput<K>) => {
      const row =
        itemId === null
          ? await apiClient.POST(listPath(kind), { params: { path: pathParams(kind, tenantId, ownerId) }, body })
          : await apiClient.PUT(itemPath(kind), { params: { path: pathParams(kind, tenantId, ownerId, itemId) }, body });
      return unwrap(row) as ContentItem<K>;
    },
    onSuccess: () => invalidateOwnedContent(tenantId, SCOPES[kind], ownerId),
  });
  return save.mutateAsync;
}

/** Deletes a row of one content kind with the rows only it gives meaning to; a 409 or 422 names what keeps it. */
export function useDeleteOwnedContent(kind: ContentKind, ownerId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (itemId: number) =>
      unwrap(await apiClient.DELETE(itemPath(kind), { params: { path: pathParams(kind, tenantId, ownerId, itemId) } })),
    onSuccess: () => invalidateOwnedContent(tenantId, SCOPES[kind], ownerId),
  });
}
