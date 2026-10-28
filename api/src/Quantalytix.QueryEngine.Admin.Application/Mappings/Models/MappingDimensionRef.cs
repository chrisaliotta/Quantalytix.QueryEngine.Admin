namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The contract's <c>MappingDimensionRef</c>: a read-only slice of a dimension, enough to label a mapping set's dimension or a
/// bridge's endpoint and to state which version entry and mapping keys resolve against.
/// </summary>
public sealed record MappingDimensionRef
{
    /// <summary>The dimension's id.</summary>
    public required long Id { get; init; }

    /// <summary>The dimension key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>A <c>dimension_purpose</c> value.</summary>
    public required string Purpose { get; init; }

    /// <summary>The live version, whose members entry and mapping keys resolve against; null while there is none.</summary>
    public required long? PublishedVersionId { get; init; }

    /// <summary>The live version's number; null while there is none.</summary>
    public required int? PublishedVersionNumber { get; init; }
}
