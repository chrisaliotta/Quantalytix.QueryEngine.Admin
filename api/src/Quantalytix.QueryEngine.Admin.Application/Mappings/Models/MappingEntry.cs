namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingEntry</c>: one source value and the member key it maps to.</summary>
public sealed record MappingEntry
{
    /// <summary>The entry's id; in a preview, the temporary id of an entry the change set created.</summary>
    public required string Id { get; init; }

    /// <summary>The mapping version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The source value, compared exactly as text (B2).</summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The key of the member it maps to.</summary>
    public required string MemberKey { get; init; }
}
