import type { components } from '@/api/schema';

type Schemas = components['schemas'];

/** A canonical dimension (`qe_dimension`). */
export type Dimension = Schemas['DimensionRecord'];

/** A named use of a dimension, its template, that bindings and calculation dimensions name (`qe_dimension_role`). */
export type DimensionRole = Schemas['DimensionRoleRecord'];

/** Body of `POST dimensions/{dimensionId}/roles`, and the first role of `POST dimensions`. */
export type NewDimensionRoleInput = Schemas['NewDimensionRoleInput'];

/** Body of `PUT dimensions/{dimensionId}/roles/{roleId}`. */
export type DimensionRoleInput = Schemas['DimensionRoleInput'];

/** A dimension version with its status and validity window (`qe_dimension_version`). */
export type DimensionVersion = Schemas['DimensionVersionRecord'];

/** A business level of a dimension version (`qe_dimension_level`). */
export type DimensionLevel = Schemas['DimensionLevelRecord'];

/** A member of a dimension version's tree (`qe_dimension_member`). */
export type DimensionMember = Schemas['DimensionMemberRecord'];

/** A typed band of a range dimension version (`qe_range_band`). */
export type RangeBand = Schemas['RangeBandRecord'];

/** Body of `POST dimensions`. */
export type NewDimensionInput = Schemas['NewDimensionInput'];

/** Body of `PUT dimensions/{dimensionId}`. */
export type DimensionInput = Schemas['DimensionInput'];

/** Body of `POST dimensions/{dimensionId}/versions`. */
export type NewDimensionVersionInput = Schemas['NewDimensionVersionInput'];

/** Body of `PUT dimension-versions/{dimensionVersionId}`. */
export type DimensionVersionInput = Schemas['DimensionVersionInput'];

/** Body of `POST .../levels`. */
export type NewLevelInput = Schemas['NewLevelInput'];

/** Body of `PUT .../levels/{levelId}`. */
export type LevelInput = Schemas['LevelInput'];

/** Body of `POST .../members`. */
export type NewMemberInput = Schemas['NewMemberInput'];

/** Body of `PUT .../members/{memberId}`. */
export type MemberInput = Schemas['MemberInput'];

/** One item of `PUT .../members/order`. */
export type MemberPlacement = Schemas['MemberPlacement'];

/** One band of `PUT .../range-bands`. */
export type RangeBandInput = Schemas['RangeBandInput'];
