import { bridgePath, dimensionPath, mappingSetPath } from '@/features/dimensions/paths';
import type { AssumptionPin } from './types';

/** The page of each living kind's identities, under the dimension it belongs under, and what the kind is called. */
const PIN_KINDS: Record<string, { path: (tenantId: string, pin: AssumptionPin) => string; label: string }> = {
  dimension_version: { path: (tenantId, pin) => dimensionPath(tenantId, pin.dimensionId), label: 'Dimension' },
  mapping_version: { path: (tenantId, pin) => mappingSetPath(tenantId, pin.dimensionId, pin.identityId), label: 'Mapping set' },
  bridge_version: { path: (tenantId, pin) => bridgePath(tenantId, pin.dimensionId, pin.identityId), label: 'Bridge' },
};

/** What a pin's identity is called, such as `Mapping set`. */
export function pinKindLabel(pin: AssumptionPin) {
  return PIN_KINDS[pin.kind].label;
}

/**
 * The page of a pin's identity with the pinned version selected; a report's dimension and the bridges into it open its report's
 * editor, the only place they change.
 */
export function pinLink(tenantId: string, pin: AssumptionPin) {
  if (pin.reportId != null) {
    return `/t/${tenantId}/reports/${pin.reportId}`;
  }
  return `${PIN_KINDS[pin.kind].path(tenantId, pin)}?version=${pin.versionId}`;
}
