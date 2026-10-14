import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A semantic remapping from one canonical dimension to another (`qe_bridge`). */
export type Bridge = Schemas['BridgeRecord'];

/** A bridge version: its endpoint dimension versions and allocation semantics (`qe_bridge_version`). */
export type BridgeVersion = Schemas['BridgeVersionRecord'];

/** One source-member to target-member assignment of a bridge version (`qe_bridge_mapping`). */
export type BridgeMapping = Schemas['BridgeMappingRecord'];

/** Body of `POST bridges`. */
export type NewBridgeInput = Schemas['NewBridgeInput'];

/** Body of `PUT bridges/{bridgeId}`. */
export type BridgeInput = Schemas['BridgeInput'];

/** Body of `POST bridges/{bridgeId}/versions`. */
export type NewBridgeVersionInput = Schemas['NewBridgeVersionInput'];

/** Body of `PUT bridge-versions/{versionId}`. */
export type BridgeVersionInput = Schemas['BridgeVersionInput'];

/** Answer of `POST bridges/{bridgeId}/versions`: the new draft and the mappings a clone dropped. */
export type CreatedBridgeVersion = Schemas['CreatedBridgeVersion'];

/** One item of `PUT bridge-versions/{versionId}/mappings`. */
export type BridgeMappingInput = Schemas['BridgeMappingInput'];
