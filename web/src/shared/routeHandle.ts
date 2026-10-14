import type { ReactNode } from 'react';
import type { UIMatch } from 'react-router';

/**
 * `handle` of a feature route; the app header turns every matched `crumb` into a breadcrumb.
 * @example handle: { crumb: 'Datasets' } satisfies RouteHandle
 * @example handle: { crumb: match => <DatasetCrumb id={Number(match.params.datasetId)} /> } satisfies RouteHandle
 */
export interface RouteHandle {
  crumb: string | ((match: UIMatch) => ReactNode);
}
