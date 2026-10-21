import type { AssumptionPin } from './types';

/** The page of each living kind's identities, under `/t/:tenantId`, and what the kind is called. */
const PIN_KINDS: Record<string, { path: string; label: string }> = {
  dimension_version: { path: 'dimensions', label: 'Dimension' },
  mapping_version: { path: 'mappings', label: 'Mapping set' },
  bridge_version: { path: 'bridges', label: 'Bridge' },
  calculation_model_version: { path: 'calculation-models', label: 'Calculation model' },
};

/** What a pin's identity is called, such as `Mapping set`. */
export function pinKindLabel(pin: AssumptionPin) {
  return PIN_KINDS[pin.kind].label;
}

/** The page of a pin's identity with the pinned version selected. */
export function pinLink(tenantId: string, pin: AssumptionPin) {
  return `/t/${tenantId}/${PIN_KINDS[pin.kind].path}/${pin.identityId}?version=${pin.versionId}`;
}
