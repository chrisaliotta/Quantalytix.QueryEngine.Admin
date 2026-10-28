namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetCreated</c>: a new mapping set and its empty live version 1.</summary>
public sealed record MappingSetCreated
{
    /// <summary>The new mapping set.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>Its empty live version 1.</summary>
    public required long VersionId { get; init; }
}
