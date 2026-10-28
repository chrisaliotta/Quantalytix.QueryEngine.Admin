namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingVersionSummary</c>: one version of a mapping set list item, with its entry count.</summary>
public sealed record MappingVersionSummary
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The version number within the mapping set.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value: <c>published</c> is live, <c>retired</c> frozen.</summary>
    public required string Status { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the version went live (UTC).</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>When the version was frozen (UTC).</summary>
    public required DateTime? RetiredAt { get; init; }

    /// <summary>Why the version was frozen.</summary>
    public required string? FreezeNote { get; init; }

    /// <summary>The version's entries.</summary>
    public required int EntryCount { get; init; }
}
