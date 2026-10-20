namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_source_profile</c> row: a named choice of one source per variable a report version reads.</summary>
public sealed record SourceProfileRecord
{
    /// <summary>Primary key.</summary>
    public required long SourceProfileId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>Key, unique among the version's profiles.</summary>
    public required string ProfileKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether report requests may run under the profile.</summary>
    public required bool IsEnabled { get; init; }
}
