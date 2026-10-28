using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The contract's <c>BridgeOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). One
/// change set covers an analytical bridge's identity and its live version's settings and mappings. The derived types are declared in
/// the contract's order, which the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(BridgeUpdateOperation), "updateBridge")]
[JsonDerivedType(typeof(BridgeVersionUpdateOperation), "updateBridgeVersion")]
[JsonDerivedType(typeof(BridgeMappingSaveOperation), "saveBridgeMapping")]
[JsonDerivedType(typeof(BridgeMappingDeleteOperation), "deleteBridgeMapping")]
[JsonDerivedType(typeof(BridgeMappingsReplaceOperation), "replaceBridgeMappings")]
public abstract record BridgeOperation;

/// <summary>An operation on the live version, which it names.</summary>
public abstract record BridgeVersionOperation : BridgeOperation
{
    /// <summary>The live version; another version refuses the operation.</summary>
    public required string VersionId { get; init; }
}

/// <summary><c>updateBridge</c>: changes the identity fields.</summary>
public sealed record BridgeUpdateOperation : BridgeOperation
{
    /// <summary>The fields to change.</summary>
    public required BridgePatch Patch { get; init; }
}

/// <summary><c>updateBridgeVersion</c>: changes the live version's cardinality and allocation mode in place (OI-26).</summary>
public sealed record BridgeVersionUpdateOperation : BridgeVersionOperation
{
    /// <summary>The new settings.</summary>
    public required BridgeVersionPatch Patch { get; init; }
}

/// <summary><c>saveBridgeMapping</c>: adds the mapping, or replaces the mapping with the same id.</summary>
public sealed record BridgeMappingSaveOperation : BridgeVersionOperation
{
    /// <summary>The mapping.</summary>
    public required BridgeMappingInput Input { get; init; }
}

/// <summary><c>deleteBridgeMapping</c>: removes a mapping.</summary>
public sealed record BridgeMappingDeleteOperation : BridgeVersionOperation
{
    /// <summary>The mapping, stored or temporary.</summary>
    public required string MappingId { get; init; }
}

/// <summary><c>replaceBridgeMappings</c>: replaces every mapping of the version (bulk paste).</summary>
public sealed record BridgeMappingsReplaceOperation : BridgeVersionOperation
{
    /// <summary>The complete mapping list.</summary>
    public required IReadOnlyList<BridgeMappingInput> Rows { get; init; }
}
