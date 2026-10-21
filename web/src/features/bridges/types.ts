import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A semantic remapping from one canonical dimension to another (`qe_bridge`). */
export type Bridge = Schemas['BridgeRecord'];

/**
 * A bridge with its purpose, the purpose of its target dimension: `analytical`, or `report` for the bridge a report source owns,
 * which names that source and its report.
 */
export type BridgeSummary = Schemas['BridgeSummary'];

/** A bridge version: its allocation semantics (`qe_bridge_version`). */
export type BridgeVersion = Schemas['BridgeVersionRecord'];

/** One source-member to target-member assignment of a bridge version, by member key (`qe_bridge_mapping`). */
export type BridgeMapping = Schemas['BridgeMappingRecord'];

/** Body of `POST bridges`. */
export type NewBridgeInput = Schemas['NewBridgeInput'];

/** Body of `PUT bridges/{bridgeId}`. */
export type BridgeInput = Schemas['BridgeInput'];

/** Body of `POST bridges/{bridgeId}/versions`. */
export type NewBridgeVersionInput = Schemas['NewBridgeVersionInput'];

/** Body of `PUT bridge-versions/{versionId}`. */
export type BridgeVersionInput = Schemas['BridgeVersionInput'];

/** One item of `PUT bridge-versions/{versionId}/mappings`. */
export type BridgeMappingInput = Schemas['BridgeMappingInput'];
