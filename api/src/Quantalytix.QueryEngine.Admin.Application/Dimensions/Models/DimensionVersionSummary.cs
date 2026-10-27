namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersionSummary</c>: one version of a list item, with its member count.</summary>
public sealed record DimensionVersionSummary
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The version number within the dimension.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>When the version went live (UTC).</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>When the version was frozen (UTC).</summary>
    public required DateTime? RetiredAt { get; init; }

    /// <summary>The next version number once frozen.</summary>
    public required int? SupersededBy { get; init; }

    /// <summary>Why the version was frozen.</summary>
    public required string? FreezeNote { get; init; }

    /// <summary>The version's members.</summary>
    public required int MemberCount { get; init; }
}
