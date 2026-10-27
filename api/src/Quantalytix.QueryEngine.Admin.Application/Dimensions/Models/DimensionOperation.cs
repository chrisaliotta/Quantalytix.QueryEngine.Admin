using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>DimensionOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). One
/// change set covers the dimension's identity, its roles and its live version's content. The derived types are declared in the
/// contract's order, which the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(DimensionUpdateDimensionOperation), "updateDimension")]
[JsonDerivedType(typeof(DimensionUpdateVersionOperation), "updateVersion")]
[JsonDerivedType(typeof(DimensionSaveMemberOperation), "saveMember")]
[JsonDerivedType(typeof(DimensionDeleteMemberOperation), "deleteMember")]
[JsonDerivedType(typeof(DimensionMoveMemberOperation), "moveMember")]
[JsonDerivedType(typeof(DimensionImportMembersOperation), "importMembers")]
[JsonDerivedType(typeof(DimensionClearMembersOperation), "clearMembers")]
[JsonDerivedType(typeof(DimensionSaveLevelOperation), "saveLevel")]
[JsonDerivedType(typeof(DimensionDeleteLevelOperation), "deleteLevel")]
[JsonDerivedType(typeof(DimensionReorderLevelsOperation), "reorderLevels")]
[JsonDerivedType(typeof(DimensionSaveBandsOperation), "saveBands")]
[JsonDerivedType(typeof(DimensionSaveRoleOperation), "saveRole")]
[JsonDerivedType(typeof(DimensionDeleteRoleOperation), "deleteRole")]
public abstract record DimensionOperation;

/// <summary>An operation on the live version's content, which it names.</summary>
public abstract record DimensionVersionOperation : DimensionOperation
{
    /// <summary>The live version; another version refuses the operation.</summary>
    public required string VersionId { get; init; }
}

/// <summary><c>updateDimension</c>: changes the identity fields.</summary>
public sealed record DimensionUpdateDimensionOperation : DimensionOperation
{
    /// <summary>The fields to change.</summary>
    public required DimensionPatch Patch { get; init; }
}

/// <summary><c>updateVersion</c>: changes the range data type, which clears every band bound.</summary>
public sealed record DimensionUpdateVersionOperation : DimensionVersionOperation
{
    /// <summary>The new attributes.</summary>
    public required DimensionVersionPatch Patch { get; init; }
}

/// <summary><c>saveMember</c>: creates or updates a member.</summary>
public sealed record DimensionSaveMemberOperation : DimensionVersionOperation
{
    /// <summary>The member.</summary>
    public required DimensionMemberInput Input { get; init; }
}

/// <summary><c>deleteMember</c>: removes a member nothing needs.</summary>
public sealed record DimensionDeleteMemberOperation : DimensionVersionOperation
{
    /// <summary>The member, stored or temporary.</summary>
    public required string MemberId { get; init; }
}

/// <summary><c>moveMember</c>: moves a member under another parent or among its siblings.</summary>
public sealed record DimensionMoveMemberOperation : DimensionVersionOperation
{
    /// <summary>The member, stored or temporary.</summary>
    public required string MemberId { get; init; }

    /// <summary>Where it lands.</summary>
    public required DimensionMemberPosition To { get; init; }
}

/// <summary><c>importMembers</c>: merges or replaces members from parsed rows.</summary>
public sealed record DimensionImportMembersOperation : DimensionVersionOperation
{
    /// <summary>The rows.</summary>
    public required IReadOnlyList<DimensionMemberImportRow> Rows { get; init; }

    /// <summary>A <see cref="DimensionImportModes"/> value.</summary>
    public required string Mode { get; init; }
}

/// <summary><c>clearMembers</c>: removes every member, when none is needed.</summary>
public sealed record DimensionClearMembersOperation : DimensionVersionOperation;

/// <summary><c>saveLevel</c>: creates or updates a level.</summary>
public sealed record DimensionSaveLevelOperation : DimensionVersionOperation
{
    /// <summary>The level.</summary>
    public required DimensionLevelInput Input { get; init; }
}

/// <summary><c>deleteLevel</c>: removes a level no member uses.</summary>
public sealed record DimensionDeleteLevelOperation : DimensionVersionOperation
{
    /// <summary>The level, stored or temporary.</summary>
    public required string LevelId { get; init; }
}

/// <summary><c>reorderLevels</c>: sets every level's ordinal from its position.</summary>
public sealed record DimensionReorderLevelsOperation : DimensionVersionOperation
{
    /// <summary>Every level of the version, top first.</summary>
    public required IReadOnlyList<string> OrderedIds { get; init; }
}

/// <summary><c>saveBands</c>: replaces the version's bands as a whole set (M5).</summary>
public sealed record DimensionSaveBandsOperation : DimensionVersionOperation
{
    /// <summary>The bands, in order.</summary>
    public required IReadOnlyList<DimensionRangeBand> Bands { get; init; }
}

/// <summary><c>saveRole</c>: creates or updates a role of the dimension.</summary>
public sealed record DimensionSaveRoleOperation : DimensionOperation
{
    /// <summary>The role.</summary>
    public required DimensionRoleInput Input { get; init; }
}

/// <summary><c>deleteRole</c>: removes a role nothing reads.</summary>
public sealed record DimensionDeleteRoleOperation : DimensionOperation
{
    /// <summary>The role, stored or temporary.</summary>
    public required string RoleId { get; init; }
}
