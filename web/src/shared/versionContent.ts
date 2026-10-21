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
 * The content the API serves row by row under a version, by the route segment of its kind: the version resource it belongs
 * to, its row and its create and edit body. A model version holds the metrics; a report version the sources, profiles and
 * calculation dimensions its report reads them through.
 */
export interface VersionContentTypes {
  variables: { scope: 'model-versions'; item: Schemas['VariableRecord']; input: Schemas['VariableInput'] };
  'calculated-measures': {
    scope: 'model-versions';
    item: Schemas['CalculatedMeasureRecord'];
    input: Schemas['CalculatedMeasureInput'];
  };
  'variable-sources': { scope: 'report-versions'; item: Schemas['VariableSourceRecord']; input: Schemas['VariableSourceInput'] };
  profiles: { scope: 'report-versions'; item: Schemas['SourceProfile']; input: Schemas['SourceProfileInput'] };
  'calculation-dimensions': {
    scope: 'report-versions';
    item: Schemas['CalculationDimensionRecord'];
    input: Schemas['CalculationDimensionInput'];
  };
}

/** A kind of version content. */
export type ContentKind = keyof VersionContentTypes;

/** A row of one content kind. */
export type ContentItem<K extends ContentKind> = VersionContentTypes[K]['item'];

/** The create and edit body of one content kind. */
export type ContentInput<K extends ContentKind> = VersionContentTypes[K]['input'];

/** A version resource whose content the API serves row by row. */
type ContentScope = VersionContentTypes[ContentKind]['scope'];

/** The list paths of the content kinds. */
type ContentListPath = {
  [K in ContentKind]: `/api/tenants/{tenantId}/${VersionContentTypes[K]['scope']}/{versionId}/${K}`;
}[ContentKind];

/** The item paths of the content kinds. */
type ContentItemPath = `${ContentListPath}/{itemId}`;

/** The version resource each kind's routes start with. */
const SCOPES: { [K in ContentKind]: VersionContentTypes[K]['scope'] } = {
  variables: 'model-versions',
  'calculated-measures': 'model-versions',
  'variable-sources': 'report-versions',
  profiles: 'report-versions',
  'calculation-dimensions': 'report-versions',
};

/** The pickers that list each version resource's content; a content write can change any of them, through cascades. */
const CONTENT_LOOKUPS: Record<ContentScope, LookupKind[]> = {
  'model-versions': ['variables', 'calculated-measures'],
  'report-versions': ['variable-sources', 'source-profiles', 'calculation-dimensions'],
};

// Query keys start with [resource, tenantId] so invalidateTenantQueries reaches them.
function contentKey(tenantId: string, scope: ContentScope, versionId: number, kind?: ContentKind) {
  return kind
    ? (['version-content', tenantId, scope, versionId, kind] as const)
    : (['version-content', tenantId, scope, versionId] as const);
}

function listPath(kind: ContentKind) {
  return `/api/tenants/{tenantId}/${SCOPES[kind]}/{versionId}/${kind}` as ContentListPath;
}

function itemPath(kind: ContentKind) {
  return `${listPath(kind)}/{itemId}` as ContentItemPath;
}

/** The rows of one content kind of a version, by key; `null` keeps the query idle until the version is known. */
export function useVersionContent<K extends ContentKind>(kind: K, versionId: number | null) {
  const tenantId = useTenantId();
  return useQuery({
    queryKey: contentKey(tenantId, SCOPES[kind], versionId ?? 0, kind),
    queryFn: async () => {
      const rows = unwrap(await apiClient.GET(listPath(kind), { params: { path: { tenantId, versionId: versionId as number } } }));
      return rows as ContentItem<K>[];
    },
    enabled: versionId !== null,
  });
}

/**
 * Refreshes every content kind of the version and its pickers after a write, because deletes cascade: removing a source
 * also removes its profile picks. A report design's source writes call it too, because they write sources and profile picks.
 */
export function invalidateVersionContent(tenantId: string, kind: ContentKind, versionId: number) {
  const scope = SCOPES[kind];
  const queryKeys = [contentKey(tenantId, scope, versionId), ...CONTENT_LOOKUPS[scope].map(lookup => lookupQueryKey(tenantId, lookup))];
  return Promise.all(queryKeys.map(queryKey => queryClient.invalidateQueries({ queryKey })));
}

/** Saves a sheet's row of a draft or published version: creates it when `itemId` is null, otherwise replaces the row with that ID. */
export function useSaveVersionContent<K extends ContentKind>(kind: K, versionId: number, itemId: number | null) {
  const tenantId = useTenantId();
  const save = useMutation({
    mutationFn: async (body: ContentInput<K>) => {
      const row =
        itemId === null
          ? await apiClient.POST(listPath(kind), { params: { path: { tenantId, versionId } }, body })
          : await apiClient.PUT(itemPath(kind), { params: { path: { tenantId, versionId, itemId } }, body });
      return unwrap(row) as ContentItem<K>;
    },
    onSuccess: () => invalidateVersionContent(tenantId, kind, versionId),
  });
  return save.mutateAsync;
}

/** Deletes a row of one content kind with the rows only it gives meaning to; a 409 names what keeps it. */
export function useDeleteVersionContent(kind: ContentKind, versionId: number) {
  const tenantId = useTenantId();
  return useMutation({
    mutationFn: async (itemId: number) =>
      unwrap(await apiClient.DELETE(itemPath(kind), { params: { path: { tenantId, versionId, itemId } } })),
    onSuccess: () => invalidateVersionContent(tenantId, kind, versionId),
  });
}
