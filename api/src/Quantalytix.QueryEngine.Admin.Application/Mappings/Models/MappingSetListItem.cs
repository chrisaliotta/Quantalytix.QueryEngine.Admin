namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetListItem</c>: a mapping set with its dimension, version summaries and the bindings that use it.</summary>
public sealed record MappingSetListItem
{
    /// <summary>The mapping set's id.</summary>
    public required long Id { get; init; }

    /// <summary>The mapping set key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free text naming the source system (B14).</summary>
    public required string SourceNamespace { get; init; }

    /// <summary>The dimension the entries map into, fixed after creation.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The dimension, with its live version.</summary>
    public required MappingDimensionRef Dimension { get; init; }

    /// <summary>Every version, one per snapshot holding the mapping set: the live one first, then frozen ones, newest first.</summary>
    public required IReadOnlyList<MappingVersionSummary> Versions { get; init; }

    /// <summary>The live version, which change sets edit.</summary>
    public required long LiveVersionId { get; init; }

    /// <summary>The entries of the live version.</summary>
    public required int LiveEntryCount { get; init; }

    /// <summary>The live dataset bindings that classify through the mapping set.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }
}
