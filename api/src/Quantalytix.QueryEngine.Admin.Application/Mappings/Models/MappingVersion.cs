namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingVersion</c>: live (stored <c>published</c>) or frozen (stored <c>retired</c>) (B10).</summary>
public sealed record MappingVersion
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The mapping set's id.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>The version number within the mapping set.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the version went live (UTC).</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>When the version was frozen (UTC).</summary>
    public required DateTime? RetiredAt { get; init; }

    /// <summary>Why the version was frozen.</summary>
    public required string? FreezeNote { get; init; }
}
